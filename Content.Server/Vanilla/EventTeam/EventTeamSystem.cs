using Content.Server.GameTicking;
using Content.Server.Station.Systems;
using Content.Server.Spawners.Components;
using Content.Server.Ghost.Roles.Components;
using Content.Shared.GameTicking;
using Content.Shared.Ghost.Roles.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Server.Maps;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Server.Player;
using System.Linq;
using Content.Shared.Storage;
using Robust.Shared.Serialization.Manager;
using Content.Server.Chat.Systems;

namespace Content.Server.Vanilla.EventTeam;

public sealed class EventTeamSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly MapLoaderSystem _map = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IComponentFactory _componentFactory = default!;
    [Dependency] private readonly ISerializationManager _serialization = default!;
    [Dependency] private readonly ChatSystem _chatSystem = default!;
    public override void Initialize()
    {
        base.Initialize();
    }

    public bool call(ProtoId<EventTeamPrototype> protoId, bool igonrejammer)
    {
        if (!_prototypes.TryIndex(protoId, out var prototype))
            return false;

        if (_gameTicker.RunLevel != GameRunLevel.InRound)
            return false;

        var shuttle = SpawnShuttle(prototype.ShuttlePath);
        if (shuttle == null)
            return false;

        // if(!igonrejammer)
        //     checkjammer();
        
        SpawnEventRoles(prototype, shuttle.Value);
        DispatchAnnouncement(prototype);

        return true;
    }

    // private void checkjammer()
    // {

    // }

    private EntityUid? SpawnShuttle(string shuttlePath)
    {
        var shuttleMap = _mapManager.CreateMap();
        var options = new MapLoadOptions {LoadMap = true};

        if (!_map.TryLoad(shuttleMap, shuttlePath, out var grids, options))
            return null;

        return grids.FirstOrDefault();
    }

    private void SpawnEventRoles(EventTeamPrototype proto, EntityUid shuttle)
    {
        var query = EntityQueryEnumerator<SpawnPointComponent, MetaDataComponent, TransformComponent>();
        var Regularmarkers = new List<EntityCoordinates>();
        while (query.MoveNext(out _, out var meta, out var trans))
        {
            if (trans.GridUid != shuttle)
                continue;

            if (meta.EntityPrototype!.ID == "MarkerEventRegularRole")
            {
                Regularmarkers.Add(trans.Coordinates);
            }

        }
        if (Regularmarkers.Count == 0)
            Regularmarkers.Add(Transform(shuttle).Coordinates);

        SpawnSpecialUnits(proto, shuttle);
        SpawnRegularUnits(proto, Regularmarkers);
    }

    private void SpawnSpecialUnits(EventTeamPrototype proto, EntityUid shuttle)
    {
        if (proto.SpecialUnits.Count == 0)
            return;

        foreach (var (spawnMarker, spawnEntry) in proto.SpecialUnits)
        {
            if (string.IsNullOrEmpty(spawnEntry))
                continue;
            // Если маркера нет, спавним в центре шаттла
            var coordinates = FindSpawnCoordinates(spawnMarker, shuttle);
            SpawnEntity(spawnEntry, coordinates);
        }
    }

    private EntityCoordinates FindSpawnCoordinates(EntProtoId<SpawnPointComponent> spawnMarker, EntityUid shuttle)
    {
        var query = EntityQueryEnumerator<SpawnPointComponent, MetaDataComponent, TransformComponent>();
        while (query.MoveNext(out var entity, out _, out var meta, out var trans))
        {
            if (meta.EntityPrototype!.ID != spawnMarker)
                continue;

            if (trans.GridUid != shuttle)
                continue;

            return trans.Coordinates;
        }
        return Transform(shuttle).Coordinates;
    }

    private void SpawnRegularUnits(EventTeamPrototype proto, List<EntityCoordinates> spawns)
    {

        if (string.IsNullOrEmpty(proto.RegularUnit))
            return;

        int playerCount = _playerManager.PlayerCount; 

        //сколько юнитов должно быть заспавнено исходя из формулы? Минимум 1 челик.
        int RegularUnitsCount = 1;

        if(playerCount>proto.MaxRegularUnitAmount && proto.MaxRegularUnitAmount>0)
            RegularUnitsCount = playerCount/proto.MaxRegularUnitAmount;

        int counter = Math.Min(RegularUnitsCount, proto.MaxRegularUnitAmount);

        while (counter > 0)
        {
            counter--;
            SpawnEntity(proto.RegularUnit, _random.Pick(spawns));
        }
    }
    private EntityUid SpawnEntity(string protoName, EntityCoordinates coordinates)
    {

        var uid = EntityManager.SpawnEntity(protoName, coordinates);
        if (TryComp<GhostRoleMobSpawnerComponent>(uid, out var mobSpawnerComponent) &&
            mobSpawnerComponent.Prototype != null &&
            _prototypes.TryIndex<EntityPrototype>(mobSpawnerComponent.Prototype, out var spawnObj) &&
            spawnObj.TryGetComponent<GhostRoleComponent>(out var tplGhostRoleComponent, _componentFactory))
        {
            var comp = _serialization.CreateCopy(tplGhostRoleComponent, notNullableOverride: true);
            EntityManager.AddComponent(uid, comp);
        }

        return uid;
    }
    private void DispatchAnnouncement(EventTeamPrototype proto)
    {
        if(proto.AnnouncementText == null)
            return;
        var sender = proto.Sender != null ? Loc.GetString(proto.Sender) : Loc.GetString("chat-manager-sender-announcement");
        _chatSystem.DispatchGlobalAnnouncement(
            Loc.GetString(proto.AnnouncementText),
            sender,
            playSound: true,
            proto.Sound,
            proto.AnnouncementColor
        );
    }


}