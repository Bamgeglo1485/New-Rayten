using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Vanilla.Skill;
using Content.Shared.DoAfter;
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;
using Robust.Shared.Audio.Systems; // Убедитесь, что этот using присутствует

namespace Content.Shared.SkillTrainer;

public sealed class SkillTrainerSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedAudioSystem  _audio = default!; // Проверьте, существует ли AudioSystem

    public override void Initialize()
    {
        SubscribeLocalEvent<SkillTrainerComponent, ActivateInWorldEvent>(OnActivateInWorld);
        SubscribeLocalEvent<SkillTrainerComponent, TrainEvent>(HandleTrainEvent);
    }

    private void OnActivateInWorld(EntityUid uid, SkillTrainerComponent component, ActivateInWorldEvent args)
    {
        if (!args.Handled)
        {
            StartDoAfter(args.User, component, uid);
            args.Handled = true; // помечаем событие как обработанное
        }
    }

    private void StartDoAfter(EntityUid user, SkillTrainerComponent component, EntityUid uid)
    {
        _audio.PlayPvs("/Audio/Vanilla/SkillSystem/bookpaperswish.ogg", user);

        var doAfterArgs = new DoAfterArgs(EntityManager, user, TimeSpan.FromSeconds(component.ReadTime), new TrainEvent
        {
            SkillType = component.SkillType,
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

        var skillComp = EnsureComp<SkillComponent>(args.User);

        if (AddExperience(skillComp, args.SkillType, args.SkillIncreaseAmount))
        {
            _audio.PlayPvs("/Audio/Vanilla/SkillSystem/levelup.ogg", args.User);
        }
        StartDoAfter(args.User, component, uid);
        args.Handled = true;
    }


    public bool AddExperience(SkillComponent skillComp, string skillType, int experienceAmount)
    {
        int requiredExp = 0;

        switch (skillType)
        {
            case "Chemistry":
                if (skillComp.ChemistryLevel < 2)
                {
                    requiredExp = skillComp.ChemistryLevel == 0 ? 300 : 600;

                    skillComp.ChemistryExp += experienceAmount;
                    if (skillComp.ChemistryExp >= requiredExp)
                    {
                        skillComp.ChemistryLevel++;
                        skillComp.ChemistryExp = 0; 
                        return true;
                    }

                }
                break;

            case "Medicine":
                if (skillComp.MedicineLevel < 2)
                {
                    requiredExp = skillComp.MedicineLevel == 0 ? 300 : 600;

                    skillComp.MedicineExp += experienceAmount;
                    if (skillComp.MedicineExp >= requiredExp)
                    {
                        skillComp.MedicineLevel++;
                        skillComp.MedicineExp = 0;
                        return true;
                    }

                }
                break;

            case "RangeWeapon":
                if (skillComp.RangeWeaponLevel < 2)
                {
                    requiredExp = skillComp.RangeWeaponLevel == 0 ? 300 : 600;

                    skillComp.RangeWeaponExp += experienceAmount;
                    if (skillComp.RangeWeaponExp >= requiredExp)
                    {
                        skillComp.RangeWeaponLevel++;
                        skillComp.RangeWeaponExp = 0;
                        return true;
                    }
                }
                break;
        }
        return false;
    }
}

[Serializable, NetSerializable]
public sealed partial class TrainEvent : SimpleDoAfterEvent
{
    public string SkillType { get; set; } = string.Empty;
    public int SkillIncreaseAmount { get; set; }
}
