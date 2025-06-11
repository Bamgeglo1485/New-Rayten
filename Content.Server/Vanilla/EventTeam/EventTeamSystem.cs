using Content.Server.GameTicking;
using Content.Server.Station.Systems;
using Content.Server.Spawners.Components;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Administration.Logs;
using Content.Server.Chat.Systems;
using Content.Server.Vanilla.Jammer;
using Content.Server.Communications;
using Content.Server.Popups;
using Content.Shared.Access.Systems;
using Content.Shared.Access.Components;
using Content.Shared.Communications;
using Content.Shared.GameTicking;
using Content.Shared.Ghost.Roles.Components;
using Content.Shared.Storage;
using Content.Shared.Database;
using Robust.Server.Player;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map.Components;
using Robust.Shared.Utility;
using System.Linq;
using System.Numerics;
namespace Content.Server.Vanilla.EventTeam;

public sealed class EventTeamSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly PopupSystem _popupSystem = default!;
    [Dependency] private readonly AccessReaderSystem _accessReaderSystem = default!;
    [Dependency] private readonly MapSystem _mapsystem = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly MapLoaderSystem _map = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IComponentFactory _componentFactory = default!;
    [Dependency] private readonly ISerializationManager _serialization = default!;
    [Dependency] private readonly ChatSystem _chatSystem = default!;
    [Dependency] private readonly JammerSystem _jammer = default!;
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly StationSystem _stationSystem = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CommunicationsConsoleComponent, CommunicationsConsoleCallERTMessage>(OnCallERTMessage);
        SubscribeLocalEvent<StationERTComponent, MapInitEvent>(OnStationInit);
        SubscribeLocalEvent<StationERTComponent, ComponentShutdown>(OnshipyardShutdown);
    }

    // Спавним верфь дсо
    private void OnStationInit(EntityUid uid, StationERTComponent component, MapInitEvent args)
    {

        // Post mapinit? fancy
        if (TryComp(component.Entity, out TransformComponent? xform))
        {
            component.MapEntity = xform.MapUid;
            return;
        }

        AddShipyard(uid, component);
    }
    private void OnshipyardShutdown(EntityUid uid, StationERTComponent component, ComponentShutdown args)
    {
        ClearShipyard(component);
    }
    private void AddShipyard(EntityUid station, StationERTComponent component)
    {
        DebugTools.Assert(LifeStage(station) >= EntityLifeStage.MapInitialized);
        if (component.MapEntity != null || component.Entity != null)
        {
            Log.Warning("Attempted to re-add an existing shipyard map.");
            return;
        }

        // Check for existing shipyards and just point to that
        var query = AllEntityQuery<StationERTComponent>();
        while (query.MoveNext(out var otherComp))
        {
            if (otherComp == component)
                continue;

            if (!Exists(otherComp.MapEntity) || !Exists(otherComp.Entity))
            {
                Log.Error($"Discovered invalid Shipyard component?");
                ClearShipyard(otherComp);
                continue;
            }

            component.MapEntity = otherComp.MapEntity;
            component.Entity = otherComp.Entity;
            return;
        }

        if (string.IsNullOrEmpty(component.Map.ToString()))
        {
            Log.Warning("No Shipyard map found, skipping setup.");
            return;
        }

        var map = _mapsystem.CreateMap(out var mapId);
        if (!_map.TryLoadGrid(mapId, component.Map, out var grid))
        {
            Log.Error($"Failed to set up Shipyard grid!");
            return;
        }

        if (!Exists(map))
        {
            Log.Error($"Failed to set up Shipyard map!");
            QueueDel(grid);
            return;
        }

        if (!Exists(grid))
        {
            Log.Error($"Failed to set up Shipyard grid!");
            QueueDel(map);
            return;
        }

        var xform = Transform(grid.Value);
        if (xform.ParentUid != map || xform.MapUid != map)
        {
            Log.Error($"Shipyard grid is not parented to its own map?");
            QueueDel(map);
            QueueDel(grid);
            return;
        }

        component.MapEntity = map;
        _metaData.SetEntityName(map, Loc.GetString("map-name-Shipyard"));
        component.Entity = grid;
        Log.Info($"Создана вервь ДСО, грид {ToPrettyString(grid)} на карте {ToPrettyString(map)} для станции {ToPrettyString(station)}");
    }

    private void ClearShipyard(StationERTComponent component)
    {
        QueueDel(component.Entity);
        QueueDel(component.MapEntity);
        component.Entity = null;
        component.MapEntity = null;
        component.ERTCalled = false;
    }

    private void OnCallERTMessage(EntityUid uid, CommunicationsConsoleComponent comp, CommunicationsConsoleCallERTMessage message)
    {
        var user = message.Actor;
        if (TryComp<AccessReaderComponent>(uid, out var accessReaderComponent))
        {
            if (!_accessReaderSystem.IsAllowed(user, uid, accessReaderComponent))
            {
                _popupSystem.PopupEntity(Loc.GetString("comms-console-permission-denied"), uid, message.Actor);
                return;
            }
        }
        var ev = new CommunicationConsoleCallShuttleAttemptEvent(uid, comp, user);
        RaiseLocalEvent(ref ev);
        if (ev.Cancelled)
        {
            _popupSystem.PopupEntity(ev.Reason ?? Loc.GetString("comms-console-shuttle-unavailable"), uid, message.Actor);
            return;
        }
        var station = _stationSystem.GetOwningStation(uid);
        if (!TryComp<StationERTComponent>(station, out var ertcomp))
        {
            _popupSystem.PopupEntity(Loc.GetString("comms-console-ert-protoerror"), uid, message.Actor);
            return;
        }

        if (ertcomp.ERTCalled)
        {
            _popupSystem.PopupEntity(Loc.GetString("comms-console-ert-alreadycalled"), uid, message.Actor);
            return;
        }

        station.Value.SpawnTimer(TimeSpan.FromMinutes(10f), () => call("ERT", ertcomp.Entity));

        ertcomp.ERTCalled = true;

        var sender = Loc.GetString("chat-manager-sender-announcement");
        _chatSystem.DispatchGlobalAnnouncement(
            Loc.GetString("comms-console-ert-call"),
            sender
        );
        _adminLogger.Add(LogType.Action, LogImpact.Extreme, $"{ToPrettyString(user):player} has called ERT");
    }

    public bool call(ProtoId<EventTeamPrototype> protoId, EntityUid? shipyard, bool igonrejammer = false)
    {
        if (!_prototypes.TryIndex(protoId, out var prototype))
        {
            Logger.Error($"Ошибка в протипе");
            return false;
        }

        if (_gameTicker.RunLevel != GameRunLevel.InRound)
        {
            Logger.Warning($"Мы в лобби?");
            return false;
        }

        var (isjammeractive, timetobreak) = _jammer.CheckJammer();

        if (!igonrejammer && isjammeractive)
        {
            Logger.Info($"Установлена глушилка, блокирующая спавн ");
            return false;
        }

        if (shipyard == null)
        {
            Logger.Error($"Верфи ДСО не существует");
            return false;
        }

        SpawnEventRoles(prototype, shipyard.Value);

        if (prototype.AnnouncementText == null)
            return true;

        var sender = prototype.Sender != null ? Loc.GetString(prototype.Sender) : Loc.GetString("chat-manager-sender-announcement");
        _chatSystem.DispatchGlobalAnnouncement(
            Loc.GetString(prototype.AnnouncementText),
            sender,
            playSound: true,
            prototype.Sound,
            prototype.AnnouncementColor
        );
        return true;
    }

    private void SpawnEventRoles(EventTeamPrototype proto, EntityUid shipyard)
    {
        var query = EntityQueryEnumerator<SpawnPointComponent, MetaDataComponent, TransformComponent>();
        var Regularmarkers = new List<EntityCoordinates>();
        while (query.MoveNext(out _, out var meta, out var trans))
        {
            if (trans.GridUid != shipyard)
                continue;

            if (meta.EntityPrototype!.ID == "MarkerEventRegularRole")
            {
                Regularmarkers.Add(trans.Coordinates);
            }

        }
        if (Regularmarkers.Count == 0)
            Regularmarkers.Add(Transform(shipyard).Coordinates);

        SpawnSpecialUnits(proto, shipyard);
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
            RegularUnitsCount = playerCount/proto.SpawnPerPlayers;

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
}
