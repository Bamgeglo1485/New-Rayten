using Content.Shared.GameTicking;
using Content.Shared.Station.Components;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Server.Chat.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Collections;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Audio;

namespace Content.Server.Vanilla.Blackout;

public sealed partial class BlackoutSystem : EntitySystem
{
    [Dependency] private SharedDoorSystem _doorSystem = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private ApcSystem _apcSystem = default!;
    [Dependency] private ChatSystem _chat = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundEndedEvent>(OnRoundEnded);
    }

    private SoundSpecifier _blackout_audio = new SoundPathSpecifier("/Audio/Announcements/shuttle_dock.ogg");

    private void OnRoundEnded(RoundEndedEvent args)
    {
        if (!TryGetRandomStation(out var station))
            return;

        if (station == null)
            return;

        BoltDoors(station.Value);
        TurnOffAPC(station.Value);
        _audio.PlayGlobal(_blackout_audio, Filter.Broadcast(), true);
    }

    private void BoltDoors(EntityUid station)
    {
        if (!TryComp<StationDataComponent>(station, out var stationData))
            return;

        var grids = stationData.Grids;

        foreach (var grid in grids)
        {
            if (!TryComp<MapGridComponent>(grid, out var mapGrid))
                continue;

            var doorQuery = AllEntityQuery<DoorBoltComponent, TransformComponent>();

            while (doorQuery.MoveNext(out var doorUid, out var doorBoltComp, out var transform))
            {
                if (transform.GridUid != grid)
                    continue;

                _doorSystem.SetBoltsDown((doorUid, doorBoltComp), true);
            }
        }
    }

    private void TurnOffAPC(EntityUid station)
    {
        if (!TryComp<StationDataComponent>(station, out var stationData))
            return;

        var grids = stationData.Grids;

        foreach (var grid in grids)
        {
            if (!TryComp<MapGridComponent>(grid, out var mapGrid))
                continue;

            var apcQuery = AllEntityQuery<ApcComponent, TransformComponent>();

            while (apcQuery.MoveNext(out var apcUid, out var apcComp, out var transform))
            {
                if (transform.GridUid != grid)
                    continue;

                _apcSystem.ApcToggleBreaker(apcUid, apcComp);
            }
        }
    }

    private bool TryGetRandomStation(out EntityUid? station)
    {
        var stations = new ValueList<EntityUid>(Count<StationDataComponent>());

        var query = AllEntityQuery<StationDataComponent>();

        while (query.MoveNext(out var uid, out _))
        {
            stations.Add(uid);
        }

        if (stations.Count == 0)
        {
            station = null;
            return false;
        }

        station = stations[_random.Next(stations.Count)];
        return true;
    }
}
