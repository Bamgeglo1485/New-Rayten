using System.Threading;
using Content.Server.Chat.Systems;
using Content.Server.Station.Systems;
using Content.Server.Station.Components;
using Content.Server.Shuttles.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Content.Shared.Nuke;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using Content.Server.Respawn;
namespace Content.Server.Vanilla.Nuke;

public sealed class NukeDiskTeleportSystem : EntitySystem
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly StationSystem _stationSystem = default!;
    private readonly Dictionary<EntityUid, CancellationTokenSource> _diskTimers = new();
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SpecialRespawnSystem _specialRespawn = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<NukeDiskComponent, MapInitEvent>(OnNukeDiskInit);
        SubscribeLocalEvent<NukeDiskComponent, ComponentShutdown>(OnNukeDiskRemoved);
    }

    private void OnNukeDiskInit(EntityUid uid, NukeDiskComponent component, ref MapInitEvent args)
    {
        if (_diskTimers.ContainsKey(uid))
            return;

        var tokenSource = new CancellationTokenSource();
        _diskTimers[uid] = tokenSource;

        // Проверяем диск каждые 10 секунд
        uid.SpawnRepeatingTimer(TimeSpan.FromSeconds(4), () => CheckNukeDisk(uid), tokenSource.Token);
    }


    private void OnNukeDiskRemoved(EntityUid uid, NukeDiskComponent component, ref ComponentShutdown args)
    {
        if (_diskTimers.TryGetValue(uid, out var tokenSource))
        {
            tokenSource.Cancel();
            _diskTimers.Remove(uid);
        }
    }

    private void CheckNukeDisk(EntityUid uid)
    {
        if (!_entityManager.TryGetComponent<TransformComponent>(uid, out var transform))
        {
            _diskTimers.Remove(uid);
            return;
        }

        EntityUid? gridUid = transform.GridUid;

        if (IsOnStationGrid(gridUid) || IsOnEvacShuttle(gridUid))
            return;
        _audio.PlayPvs("/Audio/Machines/Nuke/angry_beep.ogg", uid);
        _chat.TrySendInGameICMessage(uid, Loc.GetString("nukediscteleport-warning"),
            InGameICChatType.Speak, true);

        uid.SpawnTimer(TimeSpan.FromSeconds(3), () =>
        {
            if (!_entityManager.TryGetComponent<TransformComponent>(uid, out var newTransform))
                return;

            var newGridUid = newTransform.GridUid;
            if (newGridUid == null || !IsOnStationGrid(newGridUid.Value))
            {
                TeleportNukeDisk(uid);
            }
        });
    }

    private bool IsOnStationGrid(EntityUid? gridUid)
    {
        if(gridUid==null)
            return false;

        var query = EntityQueryEnumerator<StationDataComponent>();
        while (query.MoveNext(out var stationUid, out var stationData))
        {
            if (_stationSystem.GetLargestGrid(stationData) == gridUid)
                return true;
        }
        return false;
    }

    private bool IsOnEvacShuttle(EntityUid? gridUid)
    {
        if(gridUid==null)
            return false;
        foreach (var evacComp in EntityQuery<StationEmergencyShuttleComponent>())
        {
            if (evacComp.EmergencyShuttle == gridUid)
                return true;
        }
        return false;
    }

    private void TeleportNukeDisk(EntityUid uid)
    {
        if (!_entityManager.TryGetComponent<TransformComponent>(uid, out var transform))
            return;

        var query = EntityQueryEnumerator<StationDataComponent>();
        while (query.MoveNext(out var stationUid, out var stationData))
        {
            var largestGrid = _stationSystem.GetLargestGrid(stationData);
            if (largestGrid == null)
                continue;

            if (!_entityManager.TryGetComponent<TransformComponent>(largestGrid.Value, out var stationTransform))
                continue;

            // Ищем случайный безопасный тайл
            if (_specialRespawn.TryFindRandomTile(largestGrid.Value, stationUid, 10, out var targetCoords))
            {
                _audio.PlayPvs("/Audio/Magic/blink.ogg", transform.Coordinates);
                transform.Coordinates = targetCoords;
                _chat.TrySendInGameICMessage(uid, Loc.GetString("nukediscteleport-teleported"),
                    InGameICChatType.Speak, true);
                _audio.PlayPvs("/Audio/Magic/blink.ogg", uid);
                return;
            }
            else{
                _chat.TrySendInGameICMessage(uid, Loc.GetString("nukediscteleport-failurepathfinding"),
                    InGameICChatType.Speak, true);
            }
        }
    }


}
