using Content.Shared.Vanilla.Dominator;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Damage.Components;
using Robust.Shared.Containers;
using System.Linq;

namespace Content.Server.Vanilla.Dominator;

public sealed class SecuritronSystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _container = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SecurityMarkerComponent, DamageChangedEvent>(OnDamageChange);//напавшего на секьюритрона делаем опасным челом
        SubscribeLocalEvent<SecurityMarkerComponent, ComponentInit>(OnSecuritronInit);//напавшего на секьюритрона делаем опасным челом
    }

    private void OnSecuritronInit(EntityUid uid, SecurityMarkerComponent component, ComponentInit args)
    {
        //инициализируем контейнер
        component.HandCuffContainer = _container.EnsureContainer<ContainerSlot>(uid, "HandCuffContainer");

        var spawned = Spawn("Handcuffs", Transform(uid).Coordinates);

        _container.Insert(spawned, component.HandCuffContainer);
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
