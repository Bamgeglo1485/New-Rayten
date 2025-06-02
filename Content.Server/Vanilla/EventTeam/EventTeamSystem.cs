using Content.Server.GameTicking;
using Content.Server.Station.Systems;
using Content.Server.Spawners.Components;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Chat.Systems;
using Content.Server.Vanilla.Jammer;
using Content.Shared.GameTicking;
using Content.Shared.Ghost.Roles.Components;
using Content.Shared.Storage;
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
    [Dependency] private readonly MapSystem _mapsystem = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly MapLoaderSystem _map = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IComponentFactory _componentFactory = default!;
    [Dependency] private readonly ISerializationManager _serialization = default!;
    [Dependency] private readonly ChatSystem _chatSystem = default!;
    [Dependency] private readonly JammerSystem _jammer = default!;
    public override void Initialize()
    {
        base.Initialize();
    }

    public bool call(ProtoId<EventTeamPrototype> protoId, bool igonrejammer = false)
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

        if (!igonrejammer && _jammer.CheckJammer() != TimeSpan.Zero)
        {
            Logger.Info($"Установлена глушилка, блокирующая спавн ");
            return false;
        }

        var shuttle = SpawnShuttle(prototype.ShuttlePath);
        if (shuttle == null)
        {
            Logger.Error($"Не удалось заспавнить шаттл.");
            return false;
        }

        SpawnEventRoles(prototype, shuttle.Value);
        DispatchAnnouncement(prototype);

        return true;
    }

    private EntityUid? SpawnShuttle(string shuttlePath)
    {
        _mapsystem.CreateMap(out var mapId);
        var opts = DeserializationOptions.Default with {InitializeMaps = true};
        if (!_map.TryLoadGrid(mapId, new ResPath(shuttlePath), out var grid, opts))
        {
            return null;
        }

        return grid;
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
