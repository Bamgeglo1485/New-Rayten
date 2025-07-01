using Content.Shared.Vanilla.Background.eui;
using Content.Shared.Vanilla.Background;
using Content.Shared.Vanilla.Skill;
using Content.Shared.Eui;
using Content.Client.UserInterface.Systems.Character.Basicskills;
using Content.Client.UserInterface.Systems.Character.Easyskills;
using Content.Client.UserInterface.Systems.Character.Windows;
using Content.Client.Vanilla.UserInterface.Background;
using Content.Client.Eui;
using Content.Shared.IdentityManagement;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;

using System.Numerics;

namespace Content.Client.Vanilla.Background.eui;

public sealed class CharacterSheetEui : BaseEui
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly IUserInterfaceManager UIManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    private Dictionary<skillType, SkillControl> _skillControls = new Dictionary<skillType, SkillControl>();
    private Dictionary<skillType, EasyskillsControl> _easyskillsControl = new Dictionary<skillType, EasyskillsControl>();

    private CharacterWindow? _window;

    public override void HandleState(EuiStateBase state)
    {

        if (state is not CharacterSheetEuiState s)
            return;

        if (!_entManager.TryGetEntity(s.Target, out var target))
            return;


        _window = UIManager.CreateWindow<CharacterWindow>();
        LayoutContainer.SetAnchorPreset(_window, LayoutContainer.LayoutPreset.CenterTop);
        _window.TabSkill.OnPressed += SwitchToSkill;
        _window.TabBackground.OnPressed += SwitchToBackground;
        _window.TabInfo.Disabled = true;

        _window.SpriteView.SetEntity(target.Value);
        _window.NameLabel.Text = Identity.Name(target.Value, _entManager);

        UpdateSkill(target.Value);
        UpdateBackground(target.Value);
        _window.Open();
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
    private void SwitchToBackground(BaseButton.ButtonEventArgs args)
    {
        if (_window == null) return;
        _window.InfoContainer.Visible = false;
        _window.BackgroundContainer.Visible = true;
        _window.SkillContainer.Visible = false;
        _window.TabName.Text = "Предыстория";
        _window.MainScroll.SetScrollValue(Vector2.Zero);
    }
    private void UpdateBackground(EntityUid user)
    {
        if (_window == null)
            return;
        _window.BackgroundContainer.Children.Clear();

        if (_entManager.TryGetComponent<BackgroundComponent>(user, out var BackgroundComp))
        {
            if (BackgroundComp.GeneralBackground == null)
            {
                if (_prototypeManager.TryIndex(BackgroundComp.BabyBackground, out var bgProtoBaby))
                {
                    var backgroundControl = new BackgroundControl(bgProtoBaby.Name, bgProtoBaby.Description, bgProtoBaby.SponsorOnly);
                    _window.BackgroundContainer.Children.Add(backgroundControl);
                    _window.TabBackground.Disabled = false;
                }
                if (_prototypeManager.TryIndex(BackgroundComp.AdultBackground, out var bgProtoAdult))
                {
                    var backgroundControl = new BackgroundControl(bgProtoAdult.Name, bgProtoAdult.Description, bgProtoAdult.SponsorOnly);
                    _window.BackgroundContainer.Children.Add(backgroundControl);
                    _window.TabBackground.Disabled = false;
                }
            }
            else
            {
                if (_prototypeManager.TryIndex(BackgroundComp.GeneralBackground, out var bgProtoGeneral))
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

        var basicskills = new List<(skillType Skill, SkillLevel Level, int Experience)>
        {
            (skillType.RangeWeapon, SkillLevel.None, 0),
            (skillType.MeleeWeapon, SkillLevel.None, 0),
            (skillType.Medicine, SkillLevel.None, 0),
            (skillType.Chemistry, SkillLevel.None, 0),
            (skillType.Engineering, SkillLevel.None, 0),
            (skillType.Building, SkillLevel.None, 0),
            (skillType.Research, SkillLevel.None, 0),
            (skillType.Crime, SkillLevel.None, 0)
        };

        var easyskills = new List<(skillType Skill, bool have, int Experience)>
        {
            (skillType.Piloting, false, 0),
            (skillType.Botany, false, 0),
            (skillType.MusInstruments, false, 0),
            (skillType.Bureaucracy, false, 0),
            (skillType.Atmosphere, false, 0)
        };

        if (_entManager.TryGetComponent<SkillComponent>(user, out var skillComponent))
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
                (skillType.Crime, skillComponent.CrimeLevel, skillComponent.CrimeExp)
            };
            easyskills = new List<(skillType Skill, bool have, int Experience)>
            {
                (skillType.Piloting, skillComponent.Piloting, skillComponent.PilotingExp),
                (skillType.Botany, skillComponent.Botany, skillComponent.BotanyExp),
                (skillType.MusInstruments, skillComponent.MusInstruments, skillComponent.MusInstrumentsExp),
                (skillType.Bureaucracy, skillComponent.Bureaucracy, skillComponent.BureaucracyExp),
                (skillType.Atmosphere, skillComponent.Atmosphere, skillComponent.AtmosphereExp)
            };
            skillpoints = skillComponent.SkillPoints;
        }
        else
        {
            _window.Skillpointslabel.Visible = false;
        }

        gobasicskills(skillpoints, basicskills);
        goeasyskills(skillpoints, easyskills);
        if (_entManager.TryGetComponent<SkillAmnesiaComponent>(user, out var SkillAmnesiaComp))
            UpdateSkillAmnesia(SkillAmnesiaComp);
    }
    private void goeasyskills(int skillpoints, List<(skillType Skill, bool have, int Experience)> easyskills)
    {
        if (_window == null)
            return;

        foreach (var (skillName, have, experience) in easyskills)
        {
            var easyskillsControl = new EasyskillsControl(skillName, have, experience, (skillpoints > 0));

            _window.EasySkillContainer.Children.Add(easyskillsControl);

            _easyskillsControl[skillName] = easyskillsControl;
        }
    }

    private void gobasicskills(int skillpoints, List<(skillType Skill, SkillLevel Level, int Experience)> basicskills)
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

            _window.BasicSkillContainer.Children.Add(skillControl);
            _skillControls[skillName] = skillControl;
        }
    }

    private void UpdateSkillAmnesia(SkillAmnesiaComponent SkillAmnesiaComp)
    {
        if (_window == null)
            return;

        if (_skillControls.ContainsKey(SkillAmnesiaComp.skilltype))
        {
            var skillControl = _skillControls[SkillAmnesiaComp.skilltype];
            skillControl.updateamnesia(SkillAmnesiaComp.skilltype, SkillAmnesiaComp.exptorestore);
            return;
        }

        if (_easyskillsControl.ContainsKey(SkillAmnesiaComp.skilltype))
        {
            var easyskillsControl = _easyskillsControl[SkillAmnesiaComp.skilltype];
            easyskillsControl.updateamnesia(SkillAmnesiaComp.skilltype, SkillAmnesiaComp.exptorestore);
        }
    }


}
