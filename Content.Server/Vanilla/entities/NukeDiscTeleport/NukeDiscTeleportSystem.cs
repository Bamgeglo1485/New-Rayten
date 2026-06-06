using Content.Server.Chat.Systems;
using Content.Server.Station.Systems;
using Content.Server.Respawn;
using Content.Server.Shuttles.Components;
using Content.Shared.Station.Components;
using Content.Shared.Nuke;
using Content.Shared.Chat;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;
using Robust.Shared.Audio;

namespace Content.Server.Vanilla.Nuke;

public sealed partial class NukeDiskTeleportSystem : EntitySystem
{
    [Dependency] private IGameTiming _gameTiming = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private StationSystem _stationSystem = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SpecialRespawnSystem _specialRespawn = default!;
    [Dependency] private SharedTransformSystem _transformSystem = default!;
    private static readonly SoundSpecifier BlinkSound = new SoundPathSpecifier("/Audio/Magic/blink.ogg");
    private static readonly SoundSpecifier WarningSound = new SoundPathSpecifier("/Audio/Machines/Nuke/angry_beep.ogg");
    public override void Update(float frameTime)
    {
        var curTime = _gameTiming.CurTime;
        var query = EntityQueryEnumerator<NukeDiskComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var nuke, out var transform))
        {
            if (nuke.WillTpAt != null && curTime < nuke.WillTpAt)
                continue;

            if (IsOnStationGrid(transform.GridUid) || IsOnEvacShuttle(transform.GridUid))
            {
                nuke.WillTpAt = null;
                continue;
            }

            if (nuke.WillTpAt == null)
            {
                nuke.WillTpAt = curTime + TimeSpan.FromSeconds(5);
                _audio.PlayPvs(WarningSound, uid);
                _chat.TrySendInGameICMessage(uid, Loc.GetString("nukediscteleport-warning"),
                    InGameICChatType.Speak, true);
                continue;
            }

            TeleportNukeDisk(uid);
        }
    }

    private bool IsOnStationGrid(EntityUid? gridUid)
    {
        if (gridUid == null)
            return false;

        var query = EntityQueryEnumerator<StationDataComponent>();
        while (query.MoveNext(out var stationUid, out _))
        {
            if (_stationSystem.GetLargestGrid(stationUid) == gridUid)
                return true;
        }
        return false;
    }

    private bool IsOnEvacShuttle(EntityUid? gridUid)
    {
        if (gridUid == null)
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
        var query = EntityQueryEnumerator<StationDataComponent>();
        while (query.MoveNext(out var stationUid, out _))
        {
            var largestGrid = _stationSystem.GetLargestGrid(stationUid);
            if (largestGrid == null)
                continue;

            // Ищем случайный безопасный тайл
            if (_specialRespawn.TryFindRandomTile(largestGrid.Value, stationUid, 10, out var targetCoords))
            {
                _audio.PlayPvs(BlinkSound, Transform(uid).Coordinates);
                _transformSystem.SetCoordinates(uid, targetCoords);
                _chat.TrySendInGameICMessage(uid, Loc.GetString("nukediscteleport-teleported"),
                    InGameICChatType.Speak, true);
                _audio.PlayPvs(BlinkSound, uid);
                return;
            }
            else
            {
                _chat.TrySendInGameICMessage(uid, Loc.GetString("nukediscteleport-failurepathfinding"),
                    InGameICChatType.Speak, true);
            }
        }
    }

}
