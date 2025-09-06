using Robust.Shared.Map;
using Robust.Shared.Timing;
using Robust.Shared.Player;
using Content.Shared.Teleportation.Components;
using Robust.Shared.Map.Components;

namespace Content.Server.Teleportation;

public sealed class PortalMapCleanerSystem : EntitySystem
{
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<PortalMapComponent, ComponentInit>(OnPortalMapInit);
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<PortalMapComponent, MapComponent>();
        var currentTime = _timing.CurTime;

        while (query.MoveNext(out var uid, out var comp, out var mapComp))
        {
            if (currentTime < comp.NextCheckTime)
                continue;

            comp.NextCheckTime = currentTime + TimeSpan.FromSeconds(comp.UpdateRate);

            if (HasActivePlayers(mapComp.MapId))
                continue;

            _mapSystem.DeleteMap(mapComp.MapId);
        }
    }

    private bool HasActivePlayers(MapId mapId)
    {
        var playerQuery = EntityQueryEnumerator<ActorComponent, TransformComponent>();
        while (playerQuery.MoveNext(out _, out _, out var trans))
        {
            if (trans.MapID == mapId)
                return true;
        }

        return false;
    }

    private void OnPortalMapInit(Entity<PortalMapComponent> entity, ref ComponentInit args)
    {
        entity.Comp.NextCheckTime = _timing.CurTime + TimeSpan.FromSeconds(entity.Comp.UpdateRate);
    }
}
