using System.Linq;
using Content.Client.CharacterInfo;
using Content.Client.Gameplay;
using Content.Client.Stylesheets;
using Content.Client.UserInterface.Controls;
using Content.Client.UserInterface.Systems.Character.Controls;
using Content.Client.UserInterface.Systems.Character.Windows;
using Content.Client.UserInterface.Systems.Objectives.Controls;
using Content.Shared.Input;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Roles;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input.Binding;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using static Content.Client.CharacterInfo.CharacterInfoSystem;
using static Robust.Client.UserInterface.Controls.BaseButton;
using Content.Client.UserInterface.Systems.Character.Basicskills;
using Content.Client.UserInterface.Systems.Character.Easyskills;
using System.Numerics;
using Content.Shared.Vanilla.Skill;

namespace Content.Client.UserInterface.Systems.Character;

[UsedImplicitly]
public sealed class CharacterUIController : UIController, IOnStateEntered<GameplayState>, IOnStateExited<GameplayState>, IOnSystemChanged<CharacterInfoSystem>
{
    [Dependency] private readonly IEntityManager _ent = default!;
    [Dependency] private readonly ILogManager _logMan = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    [UISystemDependency] private readonly CharacterInfoSystem _characterInfo = default!;
    [UISystemDependency] private readonly SpriteSystem _sprite = default!;

    private ISawmill _sawmill = default!;

    private Dictionary<skillType, SkillControl> _skillControls = new Dictionary<skillType, SkillControl>();
    private Dictionary<skillType, EasyskillsControl> _easyskillsControl = new Dictionary<skillType, EasyskillsControl>();

    public override void Initialize()
    {
        base.Initialize();

        _sawmill = _logMan.GetSawmill("character");

        SubscribeNetworkEvent<MindRoleTypeChangedEvent>(OnRoleTypeChanged);
    }

    private CharacterWindow? _window;
    private MenuButton? CharacterButton => UIManager.GetActiveUIWidgetOrNull<MenuBar.Widgets.GameTopMenuBar>()?.CharacterButton;

    public void OnStateEntered(GameplayState state)
    {
        DebugTools.Assert(_window == null);

        _window = UIManager.CreateWindow<CharacterWindow>();
        LayoutContainer.SetAnchorPreset(_window, LayoutContainer.LayoutPreset.CenterTop);
        _window.TabSkill.OnPressed += SwitchToSkill;
        _window.TabInfo.OnPressed += SwitchToInfo;


        CommandBinds.Builder
            .Bind(ContentKeyFunctions.OpenCharacterMenu,
                 InputCmdHandler.FromDelegate(_ => ToggleWindow()))
             .Register<CharacterUIController>();
    }

    public void OnStateExited(GameplayState state)
    {
        if (_window != null)
        {
            _window.TabInfo.OnPressed -= SwitchToInfo;
            _window.TabSkill.OnPressed -= SwitchToSkill;
            _window.Dispose();
            _window = null;
        }

        CommandBinds.Unregister<CharacterUIController>();
    }

    public void OnSystemLoaded(CharacterInfoSystem system)
    {
        system.OnCharacterUpdate += CharacterUpdated;
        system.onskillupdateUI += UpdateSkill;
        _player.LocalPlayerDetached += CharacterDetached;
    }

    public void OnSystemUnloaded(CharacterInfoSystem system)
    {
        system.OnCharacterUpdate -= CharacterUpdated;
        system.onskillupdateUI -= UpdateSkill;
        _player.LocalPlayerDetached -= CharacterDetached;
    }

    public void UnloadButton()
    {
        if (CharacterButton == null)
        {
            return;
        }

        CharacterButton.OnPressed -= CharacterButtonPressed;
    }

    public void LoadButton()
    {
        if (CharacterButton == null)
        {
            return;
        }

        CharacterButton.OnPressed += CharacterButtonPressed;

        if (_window == null)
        {
            return;
        }

        _window.OnClose += DeactivateButton;
        _window.OnOpen += ActivateButton;
    }

    private void DeactivateButton() => CharacterButton!.Pressed = false;
    private void ActivateButton() => CharacterButton!.Pressed = true;
    private void SwitchToSkill(BaseButton.ButtonEventArgs args)
    {
        if (_window == null) return;
        _window.InfoContainer.Visible = false;
        _window.SkillContainer.Visible = true;
        _window.TabName.Text = "Навыки";
        _window.MainScroll.SetScrollValue(Vector2.Zero);
        
    }
    private void SwitchToInfo(BaseButton.ButtonEventArgs args)
    {
        if (_window == null) return;
        _window.InfoContainer.Visible = true;
        _window.SkillContainer.Visible = false;
        _window.TabName.Text = "Информация";
        _window.MainScroll.SetScrollValue(Vector2.Zero);
    }
    
    private void CharacterUpdated(CharacterData data)
    {
        if (_window == null)
        {
            return;
        }

        var (entity, job, objectives, briefing, entityName) = data;

        _window.SpriteView.SetEntity(entity);

        UpdateRoleType();

        _window.NameLabel.Text = entityName;
        _window.SubText.Text = job;
        _window.Objectives.RemoveAllChildren();
        _window.ObjectivesLabel.Visible = objectives.Any();

        foreach (var (groupId, conditions) in objectives)
        {
            var objectiveControl = new CharacterObjectiveControl
            {
                Orientation = BoxContainer.LayoutOrientation.Vertical,
                Modulate = Color.Gray
            };


            var objectiveText = new FormattedMessage();
            objectiveText.TryAddMarkup(groupId, out _);

            var objectiveLabel = new RichTextLabel
            {
                StyleClasses = {StyleNano.StyleClassTooltipActionTitle}
            };
            objectiveLabel.SetMessage(objectiveText);

            objectiveControl.AddChild(objectiveLabel);

            foreach (var condition in conditions)
            {
                var conditionControl = new ObjectiveConditionsControl();
                conditionControl.ProgressTexture.Texture = _sprite.Frame0(condition.Icon);
                conditionControl.ProgressTexture.Progress = condition.Progress;
                var titleMessage = new FormattedMessage();
                var descriptionMessage = new FormattedMessage();
                titleMessage.AddText(condition.Title);
                descriptionMessage.AddText(condition.Description);

                conditionControl.Title.SetMessage(titleMessage);
                conditionControl.Description.SetMessage(descriptionMessage);

                objectiveControl.AddChild(conditionControl);
            }

            _window.Objectives.AddChild(objectiveControl);
        }

        if (briefing != null)
        {
            var briefingControl = new ObjectiveBriefingControl();
            var text = new FormattedMessage();
            text.PushColor(Color.Yellow);
            text.AddText(briefing);
            briefingControl.Label.SetMessage(text);
            _window.Objectives.AddChild(briefingControl);
        }

        var controls = _characterInfo.GetCharacterInfoControls(entity);
        foreach (var control in controls)
        {
            _window.Objectives.AddChild(control);
        }

        _window.RolePlaceholder.Visible = briefing == null && !controls.Any() && !objectives.Any();
        UpdateSkill(entity);
    }

    private void OnRoleTypeChanged(MindRoleTypeChangedEvent ev, EntitySessionEventArgs _)
    {
        UpdateRoleType();
    }

    private void UpdateRoleType()
    {
        if (_window == null || !_window.IsOpen)
            return;

        if (!_ent.TryGetComponent<MindContainerComponent>(_player.LocalEntity, out var container)
            || container.Mind is null)
            return;

        if (!_ent.TryGetComponent<MindComponent>(container.Mind.Value, out var mind))
            return;

        var roleText = Loc.GetString("role-type-crew-aligned-name");
        var color = Color.White;
        if (_prototypeManager.TryIndex(mind.RoleType, out var proto))
        {
            roleText = Loc.GetString(proto.Name);
            color = proto.Color;
        }
        else
            _sawmill.Error($"{_player.LocalEntity} has invalid Role Type '{mind.RoleType}'. Displaying '{roleText}' instead");

        _window.RoleType.Text = roleText;
        _window.RoleType.FontColorOverride = color;
    }

    private void CharacterDetached(EntityUid uid)
    {
        CloseWindow();
    }

    private void CharacterButtonPressed(ButtonEventArgs args)
    {
        ToggleWindow();
    }

    private void CloseWindow()
    {
        _window?.Close();
    }

    private void ToggleWindow()
    {
        if (_window == null)
            return;

        if (CharacterButton != null)
        {
            CharacterButton.SetClickPressed(!_window.IsOpen);
        }

        if (_window.IsOpen)
        {
            CloseWindow();
        }
        else
        {
            _characterInfo.RequestCharacterInfo();
            _window.Open();
        }
    }

    public void UpdateSkill(EntityUid user)
    {
        if(_window==null)
            return;
        int skillpoints=0;
        _window.BasicSkillContainer.Children.Clear();
        _window.EasySkillContainer.Children.Clear();

        var basicskills = new List<(skillType Skill, SkillLevel Level, int Experience)>
        {
            (skillType.RangeWeapon, SkillLevel.None, 0),
            (skillType.MeleeWeapon, SkillLevel.None, 0),
            (skillType.Medicine, SkillLevel.None, 0),
            (skillType.Chemistry, SkillLevel.None, 0),
            (skillType.Engineering, SkillLevel.None, 0),
            (skillType.Building, SkillLevel.None, 0),
            (skillType.Research, SkillLevel.None, 0),
            (skillType.Instrumentation, SkillLevel.None, 0)
        };

        var easyskills = new List<(skillType Skill, bool have, int Experience)>
        {
            (skillType.Piloting, false, 0),
            (skillType.Botany, false, 0),
            (skillType.MusInstruments, false, 0),
            (skillType.Bureaucracy, false, 0),
            (skillType.Thief, false, 0),
            (skillType.Stealth, false, 0)
        };

        if (EntityManager.TryGetComponent<SkillComponent>(user, out var skillComponent))
        {
            basicskills = new List<(skillType Skill, SkillLevel Level, int Experience)>
            {
                (skillType.RangeWeapon, skillComponent.RangeWeaponLevel, skillComponent.RangeWeaponExp),
                (skillType.MeleeWeapon, skillComponent.MeleeWeaponLevel, skillComponent.MeleeWeaponExp),
                (skillType.Medicine, skillComponent.MedicineLevel, skillComponent.MedicineExp),
                (skillType.Chemistry, skillComponent.ChemistryLevel, skillComponent.ChemistryExp),
                (skillType.Engineering, skillComponent.EngineeringLevel, skillComponent.EngineeringExp),
                (skillType.Building, skillComponent.BuildingLevel, skillComponent.BuildingExp),
                (skillType.Research, skillComponent.ResearchLevel, skillComponent.ResearchExp),
                (skillType.Instrumentation, skillComponent.InstrumentationLevel, skillComponent.InstrumentationExp)
            };
            easyskills = new List<(skillType Skill, bool have, int Experience)>
            {
                (skillType.Piloting, skillComponent.Piloting, skillComponent.PilotingExp),
                (skillType.Botany, skillComponent.Botany, skillComponent.BotanyExp),
                (skillType.MusInstruments, skillComponent.MusInstruments, skillComponent.MusInstrumentsExp),
                (skillType.Bureaucracy, skillComponent.Bureaucracy, skillComponent.BureaucracyExp),
                (skillType.Thief, skillComponent.Thief, skillComponent.ThiefExp),
                (skillType.Stealth, skillComponent.Stealth, skillComponent.StealthExp)
            };
            skillpoints = skillComponent.SkillPoints;
        }
        else
        {
            _window.Skillpointslabel.Visible = false;
        }

        gobasicskills(skillpoints, basicskills);
        goeasyskills(skillpoints, easyskills);
        if (EntityManager.TryGetComponent<SkillAmnesiaComponent>(user, out var SkillAmnesiaComp))
            UpdateSkillAmnesia(SkillAmnesiaComp);
    }
    private void goeasyskills(int skillpoints, List<(skillType Skill, bool have, int Experience)> easyskills)
    {
        if (_window==null)
            return;

        foreach (var (skillName, have, experience) in easyskills)
        {
            var easyskillsControl = new EasyskillsControl(skillName, have, experience, (skillpoints>0) );

            easyskillsControl.OnPressed += () => _characterInfo.SendSkillExperienceEvent(skillName);

            _window.EasySkillContainer.Children.Add(easyskillsControl);

            _easyskillsControl[skillName] = easyskillsControl;
        }
    }

    private void gobasicskills(int skillpoints, List<(skillType Skill, SkillLevel Level, int Experience)> basicskills)
    {
        if (_window==null)
            return;
        bool haveskillpoint = false;
        if(skillpoints>0)
        {
            _window.Skillpointslabel.Visible = true;
            _window.Skillpointslabel.Text = $"Очков навыков: {skillpoints}";
            haveskillpoint = true;
        }
        else
        {
            _window.Skillpointslabel.Visible = false;
        }



        foreach (var (skillName, level, experience) in basicskills)
        {
            var skillControl = new SkillControl(skillName, level, experience, haveskillpoint);
            skillControl.OnPressed += () => _characterInfo.SendSkillExperienceEvent(skillName);

            _window.BasicSkillContainer.Children.Add(skillControl);
            _skillControls[skillName] = skillControl;
        }
    }

    private void UpdateSkillAmnesia(SkillAmnesiaComponent SkillAmnesiaComp)
    {
        if(_window == null)
            return;
        
        if(_skillControls.ContainsKey(SkillAmnesiaComp.skilltype))
        {
            var skillControl = _skillControls[SkillAmnesiaComp.skilltype];
            skillControl.updateamnesia(SkillAmnesiaComp.skilltype, SkillAmnesiaComp.exptorestore);
            return;
        }

        if(_easyskillsControl.ContainsKey(SkillAmnesiaComp.skilltype))
        {
            var easyskillsControl = _easyskillsControl[SkillAmnesiaComp.skilltype];
            easyskillsControl.updateamnesia(SkillAmnesiaComp.skilltype, SkillAmnesiaComp.exptorestore);
        }
    }

}