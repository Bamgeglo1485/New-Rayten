using Content.Shared.Trigger;
using Content.Shared.Damage.Systems;
using Content.Shared.Vanilla.Games.Items.TTT;

namespace Content.Shared.Vanilla.Games.TTT.Items;

public sealed class TTTExplodeOnTrigger : XOnTriggerSystem<TTTExplodeOnTriggerComponent>
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    protected override void OnTrigger(Entity<TTTExplodeOnTriggerComponent> ent, EntityUid target, ref TriggerEvent args)
    {
        EntityUid? origin = null;
        if (TryComp<TTTBombComponent>(ent, out var bomb))
            origin = bomb.User;

        var victims = _lookup.GetEntitiesInRange<TTTMarkerComponent>(Transform(ent.Owner).Coordinates, ent.Comp.Range);
        foreach (var victim in victims)
            _damageable.TryChangeDamage(victim.Owner, ent.Comp.Damage, origin: origin);
        args.Handled = true;
    }
}
