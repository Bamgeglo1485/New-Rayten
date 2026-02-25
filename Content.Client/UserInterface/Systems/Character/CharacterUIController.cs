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
using Content.Shared.Vanilla.Background;
using Content.Client.Vanilla.UserInterface.Background;

namespace Content.Client.UserInterface.Systems.Character;

[UsedImplicitly]
public sealed class CharacterUIController : UIController, IOnStateEntered<GameplayState>, IOnStateExited<GameplayState>, IOnSystemChanged<CharacterInfoSystem>
{
    [Dependency] private readonly IEntityManager _ent = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    [UISystemDependency] private readonly CharacterInfoSystem _characterInfo = default!;
    [UISystemDependency] private readonly SpriteSystem _sprite = default!;

    private Dictionary<SkillType, SkillControl> _skillControls = [];
    private Dictionary<SkillType, EasyskillsControl> _easyskillsControl = [];

    public override void Initialize()
    {
        base.Initialize();

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
        _window.TabBackground.OnPressed += SwitchToBackground;


        _window.OnClose += DeactivateButton;
        _window.OnOpen += ActivateButton;

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
            _window.TabBackground.OnPressed -= SwitchToBackground;
            _window.Close();
            _window = null;
        }

        CommandBinds.Unregister<CharacterUIController>();
    }

    public void OnSystemLoaded(CharacterInfoSystem system)
    {
        system.OnCharacterUpdate += CharacterUpdated;
        system.OnskillupdateUI += UpdateSkill;
        _player.LocalPlayerDetached += CharacterDetached;
    }

    public void OnSystemUnloaded(CharacterInfoSystem system)
    {
        system.OnCharacterUpdate -= CharacterUpdated;
        system.OnskillupdateUI -= UpdateSkill;
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
    }

    private void DeactivateButton()
    {
        if (CharacterButton == null)
        {
            return;
        }

        CharacterButton.Pressed = false;
    }

    private void ActivateButton()
    {
        if (CharacterButton == null)
        {
            return;
        }

        CharacterButton.Pressed = true;
    }
    private void SwitchToSkill(BaseButton.ButtonEventArgs args)
    {
        if (_window == null) return;
        _window.InfoContainer.Visible = false;
        _window.BackgroundContainer.Visible = false;
        _window.SkillContainer.Visible = true;
        _window.TabName.Text = "Навыки";
        _window.MainScroll.SetScrollValue(Vector2.Zero);
    }
    private void SwitchToInfo(BaseButton.ButtonEventArgs args)
    {
        if (_window == null) return;
        _window.InfoContainer.Visible = true;
        _window.SkillContainer.Visible = false;
        _window.BackgroundContainer.Visible = false;
        _window.TabName.Text = "Информация";
        _window.MainScroll.SetScrollValue(Vector2.Zero);
    }
    private void SwitchToBackground(BaseButton.ButtonEventArgs args)
    {
        if (_window == null) return;
        _window.InfoContainer.Visible = false;
        _window.BackgroundContainer.Visible = true;
        _window.SkillContainer.Visible = false;
        _window.TabName.Text = "Предыстория";
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
                StyleClasses = { StyleClass.TooltipTitle }
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
        UpdateBackground(entity);
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

        if (!_prototypeManager.TryIndex(mind.RoleType, out var proto))
            Log.Error($"Player '{_player.LocalSession}' has invalid Role Type '{mind.RoleType}'. Displaying default instead");

        _window.RoleType.Text = Loc.GetString(proto?.Name ?? "role-type-crew-aligned-name");
        _window.RoleType.FontColorOverride = proto?.Color ?? Color.White;
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

        CharacterButton?.SetClickPressed(!_window.IsOpen);

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
    private void UpdateBackground(EntityUid user)
    {
        if (_window == null)
            return;
        _window.BackgroundContainer.Children.Clear();

        if (EntityManager.TryGetComponent<BackgroundComponent>(user, out var backgroundComp))
        {
            if (backgroundComp.GeneralBackground == null)
            {
                if (_prototypeManager.TryIndex(backgroundComp.BabyBackground, out var bgProtoBaby))
                {
                    var backgroundControl = new BackgroundControl(bgProtoBaby.Name, bgProtoBaby.Description, bgProtoBaby.SponsorOnly);
                    _window.BackgroundContainer.Children.Add(backgroundControl);
                    _window.TabBackground.Disabled = false;
                }
                if (_prototypeManager.TryIndex(backgroundComp.AdultBackground, out var bgProtoAdult))
                {
                    var backgroundControl = new BackgroundControl(bgProtoAdult.Name, bgProtoAdult.Description, bgProtoAdult.SponsorOnly);
                    _window.BackgroundContainer.Children.Add(backgroundControl);
                    _window.TabBackground.Disabled = false;
                }
            }
            else
            {
                if (_prototypeManager.TryIndex(backgroundComp.GeneralBackground, out var bgProtoGeneral))
                {
                    var backgroundControl = new BackgroundControl(bgProtoGeneral.Name, bgProtoGeneral.Description, bgProtoGeneral.SponsorOnly);
                    _window.BackgroundContainer.Children.Add(backgroundControl);
                    _window.TabBackground.Disabled = false;
                }
            }

        }
        else
        {
            _window.TabBackground.Disabled = true;
        }
    }
    public void UpdateSkill(EntityUid user)
    {
        if (_window == null)
            return;

        int skillpoints = 0;
        _window.BasicSkillContainer.Children.Clear();
        _window.EasySkillContainer.Children.Clear();

        var basicSkills = new List<(SkillType Skill, SkillLevel Level, int Exp)>();
        var easySkills = new List<(SkillType Skill, bool Have, int Exp)>();

        if (EntityManager.TryGetComponent<SkillComponent>(user, out var skillComponent))
            skillpoints = skillComponent.SkillPoints;
        else
            _window.Skillpointslabel.Visible = false;

        foreach (var skill in Enum.GetValues<SkillType>())
        {
            switch (skill.GetKind())
            {
                case SkillKind.Basic:
                    basicSkills.Add((
                        skill,
                        skillComponent?.BasicSkills.GetValueOrDefault(skill, SkillLevel.None) ?? SkillLevel.None,
                        skillComponent?.SkillExps.GetValueOrDefault(skill, 0) ?? 0
                    ));
                    break;

                case SkillKind.Easy:
                    easySkills.Add((
                        skill,
                        skillComponent?.EasySkills.Contains(skill) ?? false,
                        skillComponent?.SkillExps.GetValueOrDefault(skill, 0) ?? 0
                    ));
                    break;
            }
        }

        BuildBasicSkills(skillpoints, basicSkills);
        BuildEasySkills(skillpoints, easySkills);
        if (EntityManager.TryGetComponent<SkillAmnesiaComponent>(user, out var skillAmnesiaComp))
            UpdateSkillAmnesia(skillAmnesiaComp);
    }

    private void BuildEasySkills(int skillpoints, List<(SkillType Skill, bool have, int Experience)> easyskills)
    {
        if (_window == null)
            return;

        foreach (var (skillName, have, experience) in easyskills)
        {
            var easyskillsControl = new EasyskillsControl(skillName, have, experience, (skillpoints > 0));

            easyskillsControl.OnPressed += () => _characterInfo.SendSkillExperienceEvent(skillName);

            _window.EasySkillContainer.Children.Add(easyskillsControl);

            _easyskillsControl[skillName] = easyskillsControl;
        }
    }

    private void BuildBasicSkills(int skillpoints, List<(SkillType Skill, SkillLevel Level, int Experience)> basicskills)
    {
        if (_window == null)
            return;

        bool haveskillpoint = false;

        if (skillpoints > 0)
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

    private void UpdateSkillAmnesia(SkillAmnesiaComponent skillAmnesiaComp)
    {
        if (_window == null)
            return;

        if (_skillControls.ContainsKey(skillAmnesiaComp.Skilltype))
        {
            var skillControl = _skillControls[skillAmnesiaComp.Skilltype];
            skillControl.updateamnesia(skillAmnesiaComp.Skilltype, skillAmnesiaComp.Exptorestore);
            return;
        }

        if (_easyskillsControl.ContainsKey(skillAmnesiaComp.Skilltype))
        {
            var easyskillsControl = _easyskillsControl[skillAmnesiaComp.Skilltype];
            easyskillsControl.updateamnesia(skillAmnesiaComp.Skilltype, skillAmnesiaComp.Exptorestore);
        }
    }

}
