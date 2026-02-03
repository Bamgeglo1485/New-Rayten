using System.Numerics;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Throwing;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;

namespace Content.Shared.Vanilla.Archon.SuperBall;

public sealed class SuperBallSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SuperBallComponent, StartCollideEvent>(OnCollide);
    }

    private void OnCollide(EntityUid uid, SuperBallComponent comp, ref StartCollideEvent args)
    {
        if (!TryComp<PhysicsComponent>(uid, out var physics))
            return;

        if (TryComp<ThrownItemComponent>(uid, out var throwComp))
            throwComp.Thrower = null;

        var vel = physics.LinearVelocity;
        var speed = vel.Length();

        if (speed < comp.DamageMinSpeed)
        {
            if (speed > 10f)
                _audio.PlayPvs(comp.MediumSpeedSounds, uid);
            else
                _audio.PlayPvs(comp.LowSpeedSounds, uid);

            return;
        }

        _audio.PlayPvs(comp.HighSpeedSounds, uid);
        // Критическая скорость — экстренная стабилизация
        if (speed > comp.MaxSpeed)
            _physics.SetLinearVelocity(uid, physics.LinearVelocity / speed * comp.MaxSpeed, body: physics);

        // Урон от скорости
        var damage = new DamageSpecifier
        {
            DamageDict =
            {
                { "Piercing", speed }
            }
        };

        _damage.TryChangeDamage(
            args.OtherEntity,
            damage,
            origin: uid);
    }
}
