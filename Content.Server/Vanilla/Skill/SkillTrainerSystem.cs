using Content.Shared.Interaction.Events;
using Content.Shared.Vanilla.Skill;
using Content.Shared.DoAfter;
using Robust.Shared.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Audio;
using Content.Shared.SkillTrainer;
using Content.Shared.Mobs.Components;
using Content.Shared.Ghost;
using Content.Shared.Popups;
using Content.Shared.Vanilla.Skill;
namespace Content.Server.SkillTrainer;

public sealed class ServerSkillTrainerSystem : EntitySystem
{
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] protected readonly SharedPopupSystem _popup = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SkillTrainerComponent, UseInHandEvent>(OnActivateInWorld);
        SubscribeLocalEvent<SkillTrainerComponent, TrainEvent>(HandleTrainEvent);
    }

    private void OnActivateInWorld(EntityUid uid, SkillTrainerComponent component, UseInHandEvent args)
    {
        if (!HasComp<MobStateComponent>(args.User) || HasComp<GhostComponent>(args.User))
        return;
        if(EntityManager.TryGetComponent<SkillComponent>(args.User, out var skillComp)){
            switch (component.SkillType)
            {
                case "Chemistry":
                    if(skillComp.ChemistryLevel>=component.MaxLevel){
                        _popup.PopupEntity(Loc.GetString("Skill-train-overtrain-chemistry"), args.User, args.User);
                        return;
                    }
                    break;
                case "Medicine":
                    if(skillComp.MedicineLevel>=component.MaxLevel){
                        _popup.PopupEntity(Loc.GetString("Skill-train-overtrain-medicine"), args.User, args.User);
                        return;
                    }
                    break;
                case "RangeWeapon":
                    if(skillComp.RangeWeaponLevel>=component.MaxLevel){
                        _popup.PopupEntity(Loc.GetString("Skill-train-overtrain-rangeweapon"), args.User, args.User);
                        return;
                    }
                    break;
                case "Piloting":
                    if(skillComp.PilotingLevel>=component.MaxLevel){
                        _popup.PopupEntity(Loc.GetString("Skill-train-overtrain-piloting"), args.User, args.User);
                        return;
                    }
                    break;
            }
        }
        else
            skillComp = EnsureComp<SkillComponent>(args.User);

        if (!args.Handled)
        {
            StartDoAfter(args.User, component, uid);
            args.Handled = true;
        }
    }

    private void StartDoAfter(EntityUid user, SkillTrainerComponent component, EntityUid uid)
    {
        _audio.PlayPvs("/Audio/Vanilla/SkillSystem/bookpaperswish.ogg", user, AudioParams.Default.WithMaxDistance(2f));
        var doAfterArgs = new DoAfterArgs(EntityManager, user, TimeSpan.FromSeconds(component.ReadTime), new TrainEvent
        {
            SkillType = component.SkillType,
            MaxLevel = component.MaxLevel,
            SkillIncreaseAmount = component.SkillIncreaseAmount
        }, eventTarget: uid)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            NeedHand = true,
        };

        _doAfter.TryStartDoAfter(doAfterArgs);
    }

    private void HandleTrainEvent(EntityUid uid, SkillTrainerComponent component, TrainEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        if (!EntityManager.TryGetComponent<SkillComponent>(args.User, out var skillComp))
            skillComp = EnsureComp<SkillComponent>(args.User);

        if(AddExperience(skillComp, args.SkillType, args.SkillIncreaseAmount, args.MaxLevel))
            _audio.PlayPvs("/Audio/Vanilla/SkillSystem/levelup.ogg", args.User, AudioParams.Default.WithMaxDistance(3f));
        else
            StartDoAfter(args.User, component, uid);
        args.Handled = true;
    }

    public bool AddExperience(SkillComponent skillComp, string skillType, int experienceAmount, int MaxLevel)
    {
        int requiredExp = 0;

        switch (skillType)
        {
            case "Chemistry":
                if (skillComp.ChemistryLevel < MaxLevel && skillComp.ChemistryLevel < 3)
                {
                    requiredExp = skillComp.ChemistryLevel + 300 + skillComp.ChemistryLevel * 300;

                    skillComp.ChemistryExp += experienceAmount;
                    if (skillComp.ChemistryExp >= requiredExp)
                    {
                        skillComp.ChemistryLevel++;
                        skillComp.ChemistryExp = 0;
                        skillComp.Dirty();
                        return true;
                    }
                    skillComp.Dirty();
                }
                break;

            case "Medicine":
                if (skillComp.MedicineLevel < MaxLevel  && skillComp.MedicineLevel < 3)
                {
                    requiredExp = skillComp.MedicineLevel + 300 + skillComp.MedicineLevel * 300;
                    skillComp.MedicineExp += experienceAmount;
                    if (skillComp.MedicineExp >= requiredExp)
                    {
                        skillComp.MedicineLevel++;
                        skillComp.MedicineExp = 0;
                        skillComp.Dirty();
                        return true;
                    }
                    skillComp.Dirty();
                }
                break;

            case "RangeWeapon":
                if (skillComp.RangeWeaponLevel < MaxLevel && skillComp.RangeWeaponLevel < 3)
                {
                    requiredExp = skillComp.RangeWeaponLevel + 300 + skillComp.RangeWeaponLevel * 300;
                    skillComp.RangeWeaponExp += experienceAmount;
                    if (skillComp.RangeWeaponExp >= requiredExp)
                    {
                        skillComp.RangeWeaponLevel++;
                        skillComp.RangeWeaponExp = 0;
                        skillComp.Dirty();
                        return true;
                    }
                    skillComp.Dirty();
                }
                break;

            case "Piloting":
                if (skillComp.PilotingLevel < MaxLevel && skillComp.PilotingLevel < 3)
                {
                    requiredExp = skillComp.PilotingLevel + 300 + skillComp.PilotingLevel * 300;
                    skillComp.PilotingExp += experienceAmount;
                    if (skillComp.PilotingExp >= requiredExp)
                    {
                        skillComp.PilotingLevel++;
                        skillComp.PilotingExp = 0;
                        skillComp.Dirty();
                        return true;
                    }
                    skillComp.Dirty();
                }
                break;
        }
        return false;
    }

}
