using Content.Shared.AlternateDimension;
using Content.Shared.Teleportation.Components;
using Content.Shared.Teleportation.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server.AlternateDimension;

public sealed partial class AlternateDimensionSystem
{
    [Dependency] private LinkedEntitySystem _link = default!;
    [Dependency] private SharedTransformSystem _transformSystem = default!;
    [Dependency] private IEntityManager _entityManager = default!;

    private void InitializePortal()
    {
        SubscribeLocalEvent<AlternateDimensionAutoPortalComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<AlternateDimensionAutoPortalComponent> ent, ref MapInitEvent args)
    {
        var xform = Transform(ent);

        if (xform.GridUid is null)
            return;

        if (HasComp<AlternateDimensionGridComponent>(xform.GridUid.Value))
        {
            TryCreateAndLinkPortal(ent, GetOriginalRealityCoordinates(ent));
            return;
        }

        TryCreateAndLinkPortal(ent, GetAlternateRealityCoordinates(ent, ent.Comp.TargetDimension));
    }

    private void TryCreateAndLinkPortal(Entity<AlternateDimensionAutoPortalComponent> ent, EntityCoordinates? coord)
    {
        if (coord is null)
            return;

        if (!coord.Value.IsValid(_entityManager))
            return;

        var otherEnt = SpawnAtPosition(ent.Comp.OtherSidePortal, coord.Value);

        _link.TryLink(otherEnt, ent, true);

        if (TryComp<PortalComponent>(ent, out var portal1))
        {
            portal1.CanTeleportToOtherMaps = true;
        }
        if (TryComp<PortalComponent>(otherEnt, out var portal2))
        {
            portal2.CanTeleportToOtherMaps = true;
        }

        foreach (var entity in _lookup.GetEntitiesIntersecting(coord.Value))
        {
            if (entity == otherEnt)
                continue;
            QueueDel(entity);
        }
    }
}
