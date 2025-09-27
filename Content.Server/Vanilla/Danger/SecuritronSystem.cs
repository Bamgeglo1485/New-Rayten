using Content.Shared.Vanilla.Dominator;
using Content.Shared.Damage;
using System.Linq;

namespace Content.Server.Vanilla.Dominator;

public sealed class SecuritronSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SecurityMarkerComponent, DamageChangedEvent>(OnDamageChange);//напавшего на секьюритрона делаем опасным челом
    }

    private void OnDamageChange(EntityUid uid, SecurityMarkerComponent component, DamageChangedEvent args)
    {
        if (args.Origin == null)
            return;

        var source = args.Origin.Value;

        if (!args.DamageIncreased
        || args.DamageDelta == null
        || args.DamageDelta.GetTotal() <= 0
        || !TryComp<DangerMobComponent>(source, out var sourcecomp)
        || source == uid || sourcecomp.MaxDanger)
        {
            return;
        }

        sourcecomp.MaxDanger = true;
        source.SpawnTimer(TimeSpan.FromSeconds(30), () => { sourcecomp.MaxDanger = false; });
    }

}
