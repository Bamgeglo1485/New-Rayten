using Content.Shared.Hands;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Components;
using Content.Shared.Item;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Vanilla.Skill;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Audio;
using Content.Server.SkillTrainer;

namespace Content.Server.Vanilla.Skill;

public sealed class GunSkillsSystem : SharedGunSkillsSystem
{
    [Dependency] private readonly SharedGunSystem _gun = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly ServerSkillTrainerSystem _skillTrainerSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GunComponent, GotEquippedHandEvent>(OnHandPickUp);
        SubscribeLocalEvent<GunComponent, GotUnequippedHandEvent>(OnHandDrop);
        SubscribeLocalEvent<GunComponent, GunRefreshModifiersEvent>(OnGunRefreshModifiers);
        SubscribeLocalEvent<GunTrainerComponent, GunShotEvent>(RangeWeaponTrainOnShoot);
    }

    private void OnHandPickUp(EntityUid uid, GunComponent gunComp, GotEquippedHandEvent args)
    {   
        if(HasComp<GunIgnoreSkillComponent>(uid))
        return;

        if (!HasComp<UnskilledWeaponComponent>(uid))
            AddComp<UnskilledWeaponComponent>(uid);

        if (!TryComp<UnskilledWeaponComponent>(uid, out var unskilledComp))
        return;
        
        if (TryComp<SkillComponent>(args.User, out var skillComp))
        {
            UnskilledWeaponRefreshModifiers(skillComp, unskilledComp);
        }
        else
        {
            unskilledComp.MinAnglePenalty = Angle.FromDegrees(60);
            unskilledComp.MaxAnglePenalty = Angle.FromDegrees(120);
            unskilledComp.AngleIncreasePenalty = Angle.FromDegrees(20);
        }
        _gun.RefreshModifiers(uid);
    }
    private void UnskilledWeaponRefreshModifiers(SkillComponent skillComp, UnskilledWeaponComponent unskilledComp)
    {
        switch (skillComp.RangeWeaponLevel)
        {
            case 0:
                unskilledComp.MinAnglePenalty = Angle.FromDegrees(60);
                unskilledComp.MaxAnglePenalty = Angle.FromDegrees(200);
                unskilledComp.AngleIncreasePenalty = Angle.FromDegrees(20);
                break;
            case 1:
                unskilledComp.MinAnglePenalty = Angle.FromDegrees(0);
                unskilledComp.MaxAnglePenalty = Angle.FromDegrees(50);
                unskilledComp.AngleIncreasePenalty = Angle.FromDegrees(10);
                break;
            case 2:
                unskilledComp.MinAnglePenalty = Angle.FromDegrees(0);
                unskilledComp.MaxAnglePenalty = Angle.FromDegrees(25);
                unskilledComp.AngleIncreasePenalty = Angle.FromDegrees(5);
                break;
            case 3:
                unskilledComp.MinAnglePenalty = 0;
                unskilledComp.MaxAnglePenalty = 0;
                unskilledComp.AngleIncreasePenalty = 0;
                break;
        }
    }

    private void OnHandDrop(EntityUid uid, GunComponent gunComp, GotUnequippedHandEvent args)
    {
        if (TryComp<UnskilledWeaponComponent>(uid, out var unskilledComp))
        {
            unskilledComp.MinAnglePenalty = 0;
            unskilledComp.MaxAnglePenalty = 0;
            unskilledComp.AngleIncreasePenalty = 0;
            _gun.RefreshModifiers(uid);
        }
    }

    private void OnGunRefreshModifiers(EntityUid uid, GunComponent gunComp, ref GunRefreshModifiersEvent args)
    {
        if (TryComp<UnskilledWeaponComponent>(uid, out var unskilledComp))
        {
            args.MinAngle += unskilledComp.MinAnglePenalty;
            args.MaxAngle += unskilledComp.MaxAnglePenalty;
            args.AngleIncrease += unskilledComp.AngleIncreasePenalty;
        }
    }
    private void RangeWeaponTrainOnShoot(EntityUid uid, GunTrainerComponent component, GunShotEvent args)
    {
        if (!EntityManager.TryGetComponent<SkillComponent>(args.User, out var skillComp))
            skillComp = EnsureComp<SkillComponent>(args.User);

        if (_skillTrainerSystem.AddExperience(skillComp, component.SkillType, component.ExpPerShot, component.MaxLevel)){
            if(EntityManager.TryGetComponent<UnskilledWeaponComponent>(uid, out var unskilledComp) && HasComp<GunIgnoreSkillComponent>(uid))
                UnskilledWeaponRefreshModifiers(skillComp, unskilledComp);

            _gun.RefreshModifiers(uid);
            _audio.PlayPvs("/Audio/Vanilla/SkillSystem/levelup.ogg", args.User, AudioParams.Default.WithMaxDistance(3f));
        }

    }
}
