using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Hands;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Damage.Systems;
using Robust.Shared.GameStates;
namespace Content.Shared.Vanilla.Skill;

public abstract partial class SharedSkillSystem : EntitySystem
{
    private void OnGunRefreshModifiers(EntityUid uid, UnskilledWeaponComponent unskilledComp, ref GunRefreshModifiersEvent args)
    {
        args.MinAngle += unskilledComp.MinAnglePenalty;
        args.MaxAngle += unskilledComp.MaxAnglePenalty;
        args.AngleIncrease += unskilledComp.AngleIncreasePenalty;
    }

    private void OnHandPickUp(EntityUid uid, GunComponent gunComp, GotEquippedHandEvent args)
    {
        if (TryComp<SkillComponent>(args.User, out var component))
            UpdateGun(args.User, component);
    }

    protected void UpdateGun(EntityUid uid, SkillComponent component)
    {
        if (!_hands.TryGetActiveItem(uid, out var heldEntity))
            return;

        if (HasComp<GunIgnoreSkillComponent>(heldEntity))
            return;

        var unskilledComp = EnsureComp<UnskilledWeaponComponent>(heldEntity.Value);

        if (HasComp<GunComponent>(heldEntity))
        {
            UnskilledWeaponRefreshModifiers(uid, component, unskilledComp, heldEntity.Value);
            _gun.RefreshModifiers(heldEntity.Value);
        }
    }

    public void UnskilledWeaponRefreshModifiers(EntityUid uid, SkillComponent skillComp, UnskilledWeaponComponent unskilledComp, EntityUid gun)
    {
        TryGetSkill(uid, SkillType.Weapon, out _, out var WeaponLevel, skillComp);

        switch (WeaponLevel)
        {
            case SkillLevel.None:
                unskilledComp.MinAnglePenalty = Angle.FromDegrees(50);
                unskilledComp.MaxAnglePenalty = Angle.FromDegrees(50);
                break;
            case SkillLevel.Basic:
                unskilledComp.MinAnglePenalty = Angle.FromDegrees(10);
                unskilledComp.MaxAnglePenalty = Angle.FromDegrees(10);
                break;
            case SkillLevel.Advanced:
            case SkillLevel.Expert:
                unskilledComp.MinAnglePenalty = Angle.FromDegrees(0);
                unskilledComp.MaxAnglePenalty = Angle.FromDegrees(0);
                break;
        }
        Dirty(gun, unskilledComp);
    }
}