
using System.Numerics;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Weapons.Melee.Events;

namespace Content.Shared.Vanilla.Games.TTT.Items.Knife;

public sealed class TTTBombSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedTransformSystem _transformSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TTTKnifeComponent, MeleeHitEvent>(OnMeleeHit);
    }

    private void OnMeleeHit(EntityUid uid, TTTKnifeComponent comp, ref MeleeHitEvent args)
    {
        var attacker = args.User;
        if (!HasComp<TTTTRAITORComponent>(attacker))
            return;
        foreach (var target in args.HitEntities)
        {
            if (!HasComp<TTTMarkerComponent>(target))
                continue;
            var targetPos = _transformSystem.GetWorldPosition(target);
            var attackerPos = _transformSystem.GetWorldPosition(attacker);
            var delta = attackerPos - targetPos;
            if (delta.LengthSquared() <= 0.0001f)
                continue;

            var dirToAttacker = delta.Normalized();

            var targetRotation = _transformSystem.GetWorldRotation(target);

            var targetForward = targetRotation.ToWorldVec();

            var dot = Vector2.Dot(targetForward, dirToAttacker);

            if (dot < -0.5f)
            {

                var damage = new DamageSpecifier();
                damage.DamageDict.Add("Slash", 100);

                _damageable.TryChangeDamage(target, damage);
            }
        }
    }
}
