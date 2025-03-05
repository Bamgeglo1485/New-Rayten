using Content.Server.Chat.Systems;
using Content.Server.Station.Systems;
using Content.Server.Station.Components;
using Content.Server.Shuttles.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Timing;
using Content.Shared.Nuke;
using Robust.Shared.GameObjects;
using Content.Server.Respawn;
namespace Content.Server.Vanilla.Nuke;

public sealed class NukeDiskTeleportSystem : EntitySystem
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly StationSystem _stationSystem = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SpecialRespawnSystem _specialRespawn = default!;
    [Dependency] protected readonly SharedTransformSystem TransformSystem = default!;
    public override void Initialize()
    {
        base.Initialize();
    }
    public override void Update(float frameTime)
    {
        var curTime = _gameTiming.CurTime;
        var query = EntityQueryEnumerator<NukeDiskComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var nuke, out var transform))
        {
            if(nuke.WillTpAt != null && curTime < nuke.WillTpAt)
                continue;

            if (IsOnStationGrid(transform.GridUid) || IsOnEvacShuttle(transform.GridUid))
            {
                nuke.WillTpAt = null;
                continue;
            }

            if(nuke.WillTpAt == null)
            {
                nuke.WillTpAt = curTime + TimeSpan.FromSeconds(5);
                _audio.PlayPvs("/Audio/Machines/Nuke/angry_beep.ogg", uid);
                _chat.TrySendInGameICMessage(uid, Loc.GetString("nukediscteleport-warning"),
                    InGameICChatType.Speak, true);
                continue;
            } 

            TeleportNukeDisk(uid);
        }
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
                TransformSystem.SetCoordinates(uid, targetCoords);
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
