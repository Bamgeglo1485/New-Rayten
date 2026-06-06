using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Vanilla.Skill;
using Content.Shared.Damage.Systems;
using Content.Shared.Damage.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs;
using Robust.Shared.Random;

namespace Content.Server.Vanilla.Skill;

public sealed partial class SkillSystem : SharedSkillSystem
{
    private void RangeWeaponFalldownOnShoot(EntityUid uid, GunCanBeFallComponent component, GunShotEvent args)
    {
        TryGetSkill(args.User, SkillType.Weapon, out _, out var WeaponLevel);

        if (HasComp<GunIgnoreSkillComponent>(uid))
            return;

        if (WeaponLevel < component.RequiresWeaponLevel)
        {
            float FallChance = component.RequiresWeaponLevel - WeaponLevel;

            FallChance = (FallChance > 0) ? FallChance * component.ChanceToFallPerLevel : 0;
            if (!_Random.Prob(FallChance))
                return;

            var userTransform = Transform(args.User);

            var angle = userTransform.LocalRotation;

            var offset = angle.ToWorldVec() * -component.Recoil;

            var targetCoordinates = userTransform.Coordinates.Offset(offset);

            _hands.ThrowHeldItem(args.User, targetCoordinates);
            Audio.PlayPvs(component.ThrowSound, args.User);
        }
    }
    private void OnHit(EntityUid uid, MobStateComponent component, ref DamageChangedEvent args)
    {
        if (args.Origin == null || args.Origin == uid || args.DamageDelta == null || args.DamageDelta.GetTotal() <= 0)
            return;

        if (!HasComp<StaminaComponent>(uid))
            return;

        if (component.CurrentState == MobState.Dead)
            return;

        var headshoter = args.Origin.Value;

        if (!TryComp<SkillComponent>(headshoter, out var skillcomp))
            return;

        if (!HasRequiredSkill(headshoter, SkillType.Weapon, SkillLevel.Expert, WithBeep: false, skillcomp))
            return;

        if (!_Random.Prob(HEADSHOTCHANCE))
            return;

        float staminadamage = (float)args.DamageDelta.GetTotal() * 2;
        Audio.PlayPvs(skillcomp.HeadShotSound, uid);
        if (staminadamage > 0)
            _stamina.TakeStaminaDamage(uid, staminadamage, source: headshoter, ignoreResist: false);
    }
}
