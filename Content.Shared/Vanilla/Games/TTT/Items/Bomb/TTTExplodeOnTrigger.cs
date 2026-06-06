using Content.Shared.Trigger;
using Content.Shared.Damage.Systems;
using Content.Shared.Vanilla.Games.Items.TTT;
using Content.Shared.Vanilla.Games.TTT.Items.DNAScanner;
using Content.Shared.Mobs.Systems;
namespace Content.Shared.Vanilla.Games.TTT.Items;

public sealed partial class TTTExplodeOnTrigger : XOnTriggerSystem<TTTExplodeOnTriggerComponent>
{
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private MobStateSystem _mob = default!;
    protected override void OnTrigger(Entity<TTTExplodeOnTriggerComponent> ent, EntityUid target, ref TriggerEvent args)
    {
        EntityUid? origin = null;
        if (TryComp<TTTBombComponent>(ent, out var bomb))
            origin = bomb.User;

        var victims = _lookup.GetEntitiesInRange<TTTMarkerComponent>(Transform(ent.Owner).Coordinates, ent.Comp.Range);
        foreach (var victim in victims)
        {
            if (!_mob.IsAlive(victim))
                continue;
            EnsureComp<TTTNoDnaComponent>(victim);
            _damageable.TryChangeDamage(victim.Owner, ent.Comp.Damage, origin: origin);
        }

        args.Handled = true;
    }
}
