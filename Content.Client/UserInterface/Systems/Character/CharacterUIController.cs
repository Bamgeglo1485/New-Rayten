using System.Linq;
using Content.Client.CharacterInfo;
using Content.Client.Gameplay;
using Content.Client.Stylesheets;
using Content.Client.UserInterface.Controls;
using Content.Client.UserInterface.Systems.Character.Controls;
using Content.Client.UserInterface.Systems.Character.Windows;
using Content.Client.UserInterface.Systems.Objectives.Controls;
using Content.Shared.Input;
using Content.Shared.Objectives.Systems;
using Content.Shared.Vanilla.Skill;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input.Binding;
using Robust.Shared.Network;
using Robust.Shared.Utility;
using static Content.Client.CharacterInfo.CharacterInfoSystem;
using static Robust.Client.UserInterface.Controls.BaseButton;


namespace Content.Client.UserInterface.Systems.Character;

[UsedImplicitly]
public sealed class CharacterUIController : UIController, IOnStateEntered<GameplayState>, IOnStateExited<GameplayState>, IOnSystemChanged<CharacterInfoSystem>
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [UISystemDependency] private readonly CharacterInfoSystem _characterInfo = default!;
    [UISystemDependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly INetManager _netManager = default!;

    private CharacterWindow? _window;
    private MenuButton? CharacterButton => UIManager.GetActiveUIWidgetOrNull<MenuBar.Widgets.GameTopMenuBar>()?.CharacterButton;

    public void OnStateEntered(GameplayState state)
    {
        DebugTools.Assert(_window == null);

        _window = UIManager.CreateWindow<CharacterWindow>();
        LayoutContainer.SetAnchorPreset(_window, LayoutContainer.LayoutPreset.CenterTop);

        // Подключение обработчиков к кнопкам
        _window.PilotingUpgradeButton.OnPressed += _ => _characterInfo.SendSkillExperienceEvent(skillType.Piloting);
        _window.RangeWeaponUpgradeButton.OnPressed += _ => _characterInfo.SendSkillExperienceEvent(skillType.RangeWeapon);
        _window.MeleeWeaponUpgradeButton.OnPressed += _ => _characterInfo.SendSkillExperienceEvent(skillType.MeleeWeapon);
        _window.MedicineUpgradeButton.OnPressed += _ => _characterInfo.SendSkillExperienceEvent(skillType.Medicine);
        _window.ChemistryUpgradeButton.OnPressed += _ => _characterInfo.SendSkillExperienceEvent(skillType.Chemistry);
        _window.EngineeringUpgradeButton.OnPressed += _ => _characterInfo.SendSkillExperienceEvent(skillType.Engineering);
        _window.BuildingUpgradeButton.OnPressed += _ => _characterInfo.SendSkillExperienceEvent(skillType.Building);
        _window.ResearchUpgradeButton.OnPressed += _ => _characterInfo.SendSkillExperienceEvent(skillType.Research);
        _window.InstrumentationUpgradeButton.OnPressed += _ => _characterInfo.SendSkillExperienceEvent(skillType.Instrumentation);


        CommandBinds.Builder
            .Bind(ContentKeyFunctions.OpenCharacterMenu,
                InputCmdHandler.FromDelegate(_ => ToggleWindow()))
            .Register<CharacterUIController>();
    }

    public void OnStateExited(GameplayState state)
    {
        if (_window != null)
        {
            _window.PilotingUpgradeButton.OnPressed -= _ => _characterInfo.SendSkillExperienceEvent(skillType.Piloting);
            _window.RangeWeaponUpgradeButton.OnPressed -= _ => _characterInfo.SendSkillExperienceEvent(skillType.RangeWeapon);
            _window.MeleeWeaponUpgradeButton.OnPressed -= _ => _characterInfo.SendSkillExperienceEvent(skillType.MeleeWeapon);
            _window.MedicineUpgradeButton.OnPressed -= _ => _characterInfo.SendSkillExperienceEvent(skillType.Medicine);
            _window.ChemistryUpgradeButton.OnPressed -= _ => _characterInfo.SendSkillExperienceEvent(skillType.Chemistry);
            _window.EngineeringUpgradeButton.OnPressed -= _ => _characterInfo.SendSkillExperienceEvent(skillType.Engineering);
            _window.BuildingUpgradeButton.OnPressed -= _ => _characterInfo.SendSkillExperienceEvent(skillType.Building);
            _window.ResearchUpgradeButton.OnPressed -= _ => _characterInfo.SendSkillExperienceEvent(skillType.Research);
            _window.InstrumentationUpgradeButton.OnPressed -= _ => _characterInfo.SendSkillExperienceEvent(skillType.Instrumentation);


            _window.Dispose();
            _window = null;
        }

        CommandBinds.Unregister<CharacterUIController>();
    }
    
    public void OnSystemLoaded(CharacterInfoSystem system)
    {
        system.OnCharacterUpdate += CharacterUpdated;
        system.onskillupdateUI += updateskillUI;
        _player.LocalPlayerDetached += CharacterDetached;
    }

    public void OnSystemUnloaded(CharacterInfoSystem system)
    {
        system.OnCharacterUpdate -= CharacterUpdated;
        system.onskillupdateUI -= updateskillUI;
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

    private void CharacterUpdated(CharacterData data)
    {
        if (_window == null)
        {
            return;
        }

        var (entity, job, objectives, briefing, entityName) = data;

        _window.SpriteView.SetEntity(entity);
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
        updateskillUI(entity);

    }
    private void updateskillUI(EntityUid user){

        if(_window == null)
            return;

        if (!EntityManager.TryGetComponent<SkillComponent>(user, out var skillComponent))
            return;

        UpdateProgressBars(skillComponent.PilotingLevel, skillComponent.PilotingExp, _window.Piloting1, _window.Piloting2, _window.Piloting3);
        UpdateProgressBars(skillComponent.RangeWeaponLevel, skillComponent.RangeWeaponExp, _window.RangeWeapon1, _window.RangeWeapon2, _window.RangeWeapon3);
        UpdateProgressBars(skillComponent.MeleeWeaponLevel, skillComponent.MeleeWeaponExp, _window.MeleeWeapon1, _window.MeleeWeapon2, _window.MeleeWeapon3);
        UpdateProgressBars(skillComponent.MedicineLevel, skillComponent.MedicineExp, _window.Medicine1, _window.Medicine2, _window.Medicine3);
        UpdateProgressBars(skillComponent.ChemistryLevel, skillComponent.ChemistryExp, _window.Chemistry1, _window.Chemistry2, _window.Chemistry3);
        UpdateProgressBars(skillComponent.EngineeringLevel, skillComponent.EngineeringExp, _window.Engineering1, _window.Engineering2, _window.Engineering3);
        UpdateProgressBars(skillComponent.BuildingLevel, skillComponent.BuildingExp, _window.Building1, _window.Building2, _window.Building3);
        UpdateProgressBars(skillComponent.ResearchLevel, skillComponent.ResearchExp, _window.Research1, _window.Research2, _window.Research3);
        UpdateProgressBars(skillComponent.InstrumentationLevel, skillComponent.InstrumentationExp, _window.Instrumentation1, _window.Instrumentation2, _window.Instrumentation3);

        UpdateSkillpoints(skillComponent);

        if (EntityManager.TryGetComponent<SkillAmnesiaComponent>(user, out var SkillAmnesiaComp))
            UpdateSkillAmnesia(SkillAmnesiaComp);
        else
            _window.SkillAmnesia.Visible = false;
    }

    private void UpdateSkillAmnesia(SkillAmnesiaComponent SkillAmnesiaComp)
    {
        if(_window == null)
            return;
        // Расчёт оставшегося времени на восстановление
        int remainingExperience = SkillAmnesiaComp.exptorestore;
        int totalSecondsToRestore = (remainingExperience / 3) * 2;

        // Форматирование времени
        var minutes = ((int)totalSecondsToRestore / 60).ToString("00");
        var seconds = ((int)totalSecondsToRestore % 60).ToString("00");

        // Обновление UI
        _window.SkillAmnesia.Visible = true;

        string skillName = SkillAmnesiaComp.skilltype switch
        {
            skillType.Piloting => "пилотирование",
            skillType.RangeWeapon => "стрельбу",
            skillType.MeleeWeapon => "ближний бой",
            skillType.Medicine => "медицину",
            skillType.Chemistry => "химию",
            skillType.Engineering => "инженерию",
            skillType.Building => "строительство",
            skillType.Research => "исследования",
            skillType.Instrumentation => "приборостроение",
            _ => ""
        };

        _window.SkillAmnesia.Text = 
            $"После смерти вы забыли {skillName}!\n" +
            $"{SkillAmnesiaComp.exptorestore} опыта будет восстановлено в течение {minutes}:{seconds}";

    }

    private void UpdateSkillpoints(SkillComponent skillComponent)
    {
        if(_window == null)
            return;
        if(skillComponent.SkillPoints < 1)
        {
            _window.Skillpointslabel.Visible = false;

            _window.PilotingUpgradeButton.Visible = false;
            _window.RangeWeaponUpgradeButton.Visible = false;
            _window.MeleeWeaponUpgradeButton.Visible = false;
            _window.MedicineUpgradeButton.Visible = false;
            _window.ChemistryUpgradeButton.Visible = false;
            _window.EngineeringUpgradeButton.Visible = false;
            _window.BuildingUpgradeButton.Visible = false;
            _window.ResearchUpgradeButton.Visible = false;
            _window.InstrumentationUpgradeButton.Visible = false;
            return;
        }

        _window.Skillpointslabel.Visible = true;
        _window.Skillpointslabel.Text = $"Очков навыков: {skillComponent.SkillPoints}";

        _window.PilotingUpgradeButton.Visible = skillComponent.PilotingLevel < 3;
        _window.RangeWeaponUpgradeButton.Visible = skillComponent.RangeWeaponLevel < 3;
        _window.MeleeWeaponUpgradeButton.Visible = skillComponent.MeleeWeaponLevel < 3;
        _window.MedicineUpgradeButton.Visible  = skillComponent.MedicineLevel < 3;
        _window.ChemistryUpgradeButton.Visible = skillComponent.ChemistryLevel < 3;
        _window.EngineeringUpgradeButton.Visible = skillComponent.EngineeringLevel < 3;
        _window.BuildingUpgradeButton.Visible = skillComponent.BuildingLevel < 3;
        _window.ResearchUpgradeButton.Visible = skillComponent.ResearchLevel < 3;
        _window.InstrumentationUpgradeButton.Visible = skillComponent.InstrumentationLevel < 3;
    }

    private void UpdateProgressBars(int level, int exp, ProgressBar bar1, ProgressBar bar2, ProgressBar bar3)
    {
        switch (level)
        {
            case 0:
                bar1.Value = exp;
                bar2.Value = 0;
                bar3.Value = 0;
                break;
            case 1:
                bar1.Value = 300;
                bar2.Value = exp;
                bar3.Value = 0;
                break;
            case 2:
                bar1.Value = 300;
                bar2.Value = 600;
                bar3.Value = exp;
                break;
            case 3:
                bar1.Value = 300;
                bar2.Value = 600;
                bar3.Value = 900;
                break;
            default:
                break;
        }
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
}
