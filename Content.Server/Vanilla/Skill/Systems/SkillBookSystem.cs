using Content.Shared.Interaction.Events;
using Content.Shared.Vanilla.Skill;
using Content.Shared.DoAfter;
using Content.Shared.SkillTrainer;
using Content.Shared.Mobs.Components;
using Content.Shared.Ghost;
using Content.Shared.Popups;
using Content.Shared.Vanilla.Skill;
using Content.Shared.Examine;
using Robust.Shared.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server.SkillTrainer;

public sealed class SkillBookSystem : EntitySystem
{
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly ServerSkillTrainerSystem _skillTrainerSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SkillBookComponent, UseInHandEvent>(OnUserInHand);
        SubscribeLocalEvent<SkillBookComponent, SkillBookEvent>(HandleSkillBookEvent);
        SubscribeLocalEvent<SkillBookComponent, ExaminedEvent>(OnExamine);
    }

    private void OnExamine(EntityUid uid, SkillBookComponent component, ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;
        
        if(!EntityManager.TryGetComponent<SkillLearnerComponent>(args.Examiner, out var skillComp))
            skillComp = EnsureComp<SkillLearnerComponent>(args.Examiner);

        int SkillExpToLearn = skillComp.GetSkillExpToLearn(component.SkillType);
        args.PushMarkup(Loc.GetString("examine-skilltrainer-part-1", ("skilltype", component.SkillType.ToString())));
        args.PushMarkup(Loc.GetString("examine-skilltrainer-part-2", ("SkillExpToLearn", SkillExpToLearn)));
    }

    private void OnUserInHand(EntityUid uid, SkillBookComponent component, UseInHandEvent args)
    {
        if (!HasComp<MobStateComponent>(args.User) || HasComp<GhostComponent>(args.User))
            return;

        if (!EntityManager.TryGetComponent<SkillComponent>(args.User, out var skillComp))
            skillComp = EnsureComp<SkillComponent>(args.User);
            
        if (!EntityManager.TryGetComponent<SkillLearnerComponent>(args.User, out var SkillLearnerComponentComp))
            SkillLearnerComponentComp = EnsureComp<SkillLearnerComponent>(args.User);

        if (SkillLearnerComponentComp.GetSkillExpToLearn(component.SkillType) <= 0 || skillComp.GetSkillLevel(component.SkillType) >= SkillLevel.Expert || skillComp.GetEasySkill(component.SkillType) == true)
        {
            var overtrainKey = $"Skill-train-overtrain-{component.SkillType.ToString().ToLower()}";
            _popup.PopupEntity(Loc.GetString(overtrainKey), args.User, args.User);
            return;
        }

        if (!args.Handled)
        {
            StartDoAfter(args.User, component, uid, skillComp);
            args.Handled = true;
        }
    }


    private void StartDoAfter(EntityUid user, SkillBookComponent component, EntityUid uid,SkillComponent skillComp)
    {
        _audio.PlayPvs("/Audio/Vanilla/SkillSystem/bookpaperswish.ogg", user, AudioParams.Default.WithMaxDistance(2f));
        var doAfterArgs = new DoAfterArgs(
                                        EntityManager, 
                                        user, 
                                        TimeSpan.FromSeconds(component.BaseReadTime), 
                                        new SkillBookEvent
                                        {
                                            SkillType = component.SkillType,
                                            SkillIncreaseAmount = component.SkillIncreaseAmount
                                        },
                                        eventTarget: uid
                                        )
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            NeedHand = true,
        };
        _doAfter.TryStartDoAfter(doAfterArgs);
    }

    private void HandleSkillBookEvent(EntityUid uid, SkillBookComponent component, SkillBookEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        if (!EntityManager.TryGetComponent<SkillComponent>(args.User, out var skillComp))
            skillComp = EnsureComp<SkillComponent>(args.User);

        if (!EntityManager.TryGetComponent<SkillLearnerComponent>(args.User, out var SkillLearnerComp))
            SkillLearnerComp = EnsureComp<SkillLearnerComponent>(args.User);

        int exp = DecreaseSkillExpToLearn(SkillLearnerComp, args.SkillType, args.SkillIncreaseAmount);

        if (!_skillTrainerSystem.AddExperience(skillComp, args.SkillType, exp))
        {
            if (!(SkillLearnerComp.GetSkillExpToLearn(args.SkillType) <= 0 || skillComp.GetSkillLevel(args.SkillType) >= SkillLevel.Expert))
            {
                StartDoAfter(args.User, component, uid, skillComp);
            }
        }
        args.Handled = true;
    }
    private int DecreaseSkillExpToLearn(SkillLearnerComponent skillComp, skillType skill, int exp)
    {
        if (exp < 0) return 0;

        switch (skill)
        {
            case skillType.Piloting:
                if(exp > skillComp.PilotingExpToLearn)
                    exp = skillComp.PilotingExpToLearn;
                skillComp.PilotingExpToLearn -= exp;
                break;
            case skillType.Botany:
                if(exp > skillComp.BotanyExpToLearn)
                    exp = skillComp.BotanyExpToLearn;
                skillComp.BotanyExpToLearn -= exp;
                break;
            case skillType.Bureaucracy:
                if(exp > skillComp.BureaucracyExpToLearn)
                    exp = skillComp.BureaucracyExpToLearn;
                skillComp.BureaucracyExpToLearn -= exp;
                break;
            case skillType.MusInstruments:
                if(exp > skillComp.MusInstrumentsExpToLearn)
                    exp = skillComp.MusInstrumentsExpToLearn;
                skillComp.MusInstrumentsExpToLearn -= exp;
                break;
            case skillType.Atmosphere:
                if(exp > skillComp.AtmosphereExpToLearn)
                    exp = skillComp.AtmosphereExpToLearn;
                skillComp.AtmosphereExpToLearn -= exp;
                break;
            case skillType.RangeWeapon:
                if(exp > skillComp.RangeWeaponExpToLearn)
                    exp = skillComp.RangeWeaponExpToLearn;
                skillComp.RangeWeaponExpToLearn -= exp;
                break;
            case skillType.MeleeWeapon:
                if(exp > skillComp.MeleeWeaponExpToLearn)
                    exp = skillComp.MeleeWeaponExpToLearn;
                skillComp.MeleeWeaponExpToLearn -= exp;
                break;
            case skillType.Medicine:
                if(exp > skillComp.MedicineExpToLearn)
                    exp = skillComp.MedicineExpToLearn;
                skillComp.MedicineExpToLearn -= exp;
                break;
            case skillType.Chemistry:
                if(exp > skillComp.ChemistryExpToLearn)
                    exp = skillComp.ChemistryExpToLearn;
                skillComp.ChemistryExpToLearn -= exp;
                break;
            case skillType.Engineering:
                if(exp > skillComp.EngineeringExpToLearn)
                    exp = skillComp.EngineeringExpToLearn;
                skillComp.EngineeringExpToLearn -= exp;
                break;
            case skillType.Building:
                if(exp > skillComp.BuildingExpToLearn)
                    exp = skillComp.BuildingExpToLearn;
                skillComp.BuildingExpToLearn -= exp;
                break;
            case skillType.Research:
                if(exp > skillComp.ResearchExpToLearn)
                    exp = skillComp.ResearchExpToLearn;
                skillComp.ResearchExpToLearn -= exp;
                break;
            case skillType.Crime:
                if(exp > skillComp.CrimeExpToLearn)
                    exp = skillComp.CrimeExpToLearn;
                skillComp.CrimeExpToLearn -= exp;
                break;
            default:
                break;
        }
        skillComp.Dirty();
        return exp;
    }
}