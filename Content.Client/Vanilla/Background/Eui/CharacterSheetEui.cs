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

    private Dictionary<SkillType, SkillControl> _skillControls = [];
    private Dictionary<SkillType, EasyskillsControl> _easyskillsControl = [];

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

        var basicSkills = new List<(SkillType Skill, SkillLevel Level, int Exp)>();
        var easySkills = new List<(SkillType Skill, bool Have, int Exp)>();

        if (!_entManager.TryGetComponent<SkillComponent>(user, out var skillComponent))
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
        if (_entManager.TryGetComponent<SkillAmnesiaComponent>(user, out var SkillAmnesiaComp))
            UpdateSkillAmnesia(SkillAmnesiaComp);
    }
    private void BuildEasySkills(int skillpoints, List<(SkillType Skill, bool have, int Experience)> easyskills)
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

            _window.BasicSkillContainer.Children.Add(skillControl);
            _skillControls[skillName] = skillControl;
        }
    }

    private void UpdateSkillAmnesia(SkillAmnesiaComponent SkillAmnesiaComp)
    {
        if (_window == null)
            return;

        if (_skillControls.ContainsKey(SkillAmnesiaComp.Skilltype))
        {
            var skillControl = _skillControls[SkillAmnesiaComp.Skilltype];
            skillControl.updateamnesia(SkillAmnesiaComp.Skilltype, SkillAmnesiaComp.Exptorestore);
            return;
        }

        if (_easyskillsControl.ContainsKey(SkillAmnesiaComp.Skilltype))
        {
            var easyskillsControl = _easyskillsControl[SkillAmnesiaComp.Skilltype];
            easyskillsControl.updateamnesia(SkillAmnesiaComp.Skilltype, SkillAmnesiaComp.Exptorestore);
        }
    }

}
