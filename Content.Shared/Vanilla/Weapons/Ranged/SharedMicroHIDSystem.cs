using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;
using System.Diagnostics.CodeAnalysis;

namespace Content.Shared.Vanilla.Weapons.Ranged;

public abstract class SharedMicroHIDSystem : EntitySystem
{
    [Dependency] protected readonly IGameTiming Timing = default!;
    [Dependency] protected readonly SharedBatterySystem Battery = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedStaminaSystem _stamina = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeAllEvent<WeaponChargeEvent>(OnChargeStateChange);
        SubscribeAllEvent<WeaponChargeShootRequestEvent>(OnShoot);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!Timing.IsFirstTimePredicted)
            return;

        var query = EntityQueryEnumerator<ChargedWeaponComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            if (!comp.IsShooting)
                continue;

            if (!_hands.TryGetActiveItem(xform.ParentUid, out var helditem)
                || helditem != uid)
            {
                StopShooting(uid, comp);
            }
        }
    }

    public void StartShooting(EntityUid uid, ChargedWeaponComponent comp, EntityUid? user = null)
    {
        comp.IsShooting = true;
        comp.NextShootAt = Timing.CurTime + comp.ChargeDuration;

        if (Timing.IsFirstTimePredicted)
        {
            _audio.Stop(comp.Stream);
            comp.Stream = _audio.PlayPredicted(comp.UpSound, uid, user)?.Entity;
        }
    }

    public void StopShooting(EntityUid uid, ChargedWeaponComponent comp, EntityUid? user = null)
    {
        if (!comp.IsShooting)
            return;

        comp.IsShooting = false;
        if (Timing.IsFirstTimePredicted)
        {
            _audio.Stop(comp.Stream);
            comp.Stream = _audio.PlayPredicted(comp.DownSound, uid, user)?.Entity;
        }
    }

    private void OnShoot(WeaponChargeShootRequestEvent msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } user)
            return;

        if (!TryGetChargedWeapon(user, out var weaponUid, out var chargedcomp) || weaponUid != GetEntity(msg.Weapon))
            return;

        //1. наступило ли время
        if (!chargedcomp.IsShooting || Timing.CurTime < chargedcomp.NextShootAt)
            return;
        chargedcomp.NextShootAt = Timing.CurTime + TimeSpan.FromSeconds(1f / chargedcomp.FireRate);

        if (Battery.TryUseCharge(weaponUid, chargedcomp.EnergyPerShoot))
        {
            var target = GetEntity(msg.Target);
            if (target == null)
                return;

            //2. достаточный ли ренж
            Transform(weaponUid).Coordinates.TryDistance(EntityManager, Transform(target.Value).Coordinates, out var distance);
            if (distance > chargedcomp.MaxRange)
                return;

            Shoot(user, target.Value);
            _stamina.TakeStaminaDamage(target.Value, chargedcomp.StaminaDamagePerShoot);
        }
        else
            StopShooting(weaponUid, chargedcomp, user);

    }

    private void OnChargeStateChange(WeaponChargeEvent msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } user)
            return;

        if (!TryGetChargedWeapon(user, out var weaponUid, out var chargedcomp)
            || weaponUid != GetEntity(msg.Weapon))
        {
            return;
        }

        if (!msg.StartCharge)
            StopShooting(weaponUid, chargedcomp, user);

        if (Battery.GetCharge(weaponUid) < chargedcomp.EnergyPerShoot)
            return;

        if (msg.StartCharge)
            StartShooting(weaponUid, chargedcomp, user);
    }
    public bool TryGetChargedWeapon(EntityUid entity, out EntityUid weaponUid, [NotNullWhen(true)] out ChargedWeaponComponent? charged)
    {
        weaponUid = default;
        charged = null;

        if (_hands.TryGetActiveItem(entity, out var held))
        {
            if (TryComp(held, out charged))
            {
                weaponUid = held.Value;
                return true;
            }
        }

        return false;
    }

    public virtual void Shoot(EntityUid user, EntityUid target, string proto = "MicroHidLightning")
    {
    }
}
