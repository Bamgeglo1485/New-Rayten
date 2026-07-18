using System.Linq;
using System.Numerics;
using Content.Server.Chat.Systems;
using Robust.Shared.Map;
using Robust.Shared.Collections;
using Content.Shared.Station.Components;
using Content.Server.GameTicking.Events;
using Content.Server.Pinpointer;
using Content.Server.RoundEnd;
using Content.Server.Station.Events;
using Content.Server.Station.Systems;
using Content.Server.Audio;
using Content.Shared.Access.Systems;
using Content.Shared.CCVar;
using Content.Shared.Database;
using Content.Shared.DeviceNetwork;
using Content.Shared.DeviceNetwork.Components;
using Content.Shared.GameTicking;
using Content.Shared.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using Robust.Shared.GameStates;
using Robust.Shared.Audio;
using Content.Shared.Evac.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Content.Server.Atmos.Piping.Unary.Components;
using Content.Server.Nuke;
using Content.Server.AlertLevel;
using Content.Shared.Light.Components;
using Content.Server.Power.Components;
using Content.Shared.Vanilla.Evacuation.Events;

namespace Content.Server.Vanilla.Evacuation;

public sealed partial class EvacuationSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private ChatSystem _chatSystem = default!;
    [Dependency] private RoundEndSystem _roundEnd = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private TransformSystem _transformSystem = default!;
    [Dependency] private UserInterfaceSystem _uiSystem = default!;
    [Dependency] private ServerGlobalSoundSystem _sound = default!;
    [Dependency] private SharedPointLightSystem _pointLight = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private AlertLevelSystem _alertLevelSystem = default!;
    [Dependency] private StationSystem _stationSystem = default!;

    public enum EvacState
    {
        Idle,
        Evacuation,
        PreEvacuation,
        Evacuated
    }

    private EvacState _evac_state { get; set; } = EvacState.Idle;

    private TimeSpan _evac_end = TimeSpan.Zero;
    private TimeSpan _pre_evac = TimeSpan.Zero;

    private EntityUid? _station;
    private EntityUid? _nuke;

    private SoundSpecifier _announcement = new SoundPathSpecifier("/Audio/Vanilla/Announcements/evac.ogg");
    private SoundSpecifier _music = new SoundPathSpecifier("/Audio/Vanilla/StationEvents/Rise_of_a_Second_Sun.ogg");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundCleanup);
        SubscribeLocalEvent<EvacuationRequestMessage>(OnEvacuationRequest);
    }

    private void OnEvacuationRequest(EvacuationRequestMessage args)
    {
        StartEvacuation();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        switch (_evac_state)
        {
            case EvacState.Evacuation:
                if (_timing.CurTime > _pre_evac)
                    PreEvacuation();
                break;

            case EvacState.PreEvacuation:
                if (_timing.CurTime > _evac_end)
                    EndEvacuation();
                break;

            default:
                break;
        }
    }

    public void StartEvacuation(float duration = 240f)
    {
        if (_evac_state != EvacState.Idle)
            return;

        _station = _roundEnd.GetStation();

        if (_station == null)
            return;

        _evac_state = EvacState.Evacuation;
        _evac_end = _timing.CurTime + TimeSpan.FromSeconds(duration);
        _pre_evac = _timing.CurTime + TimeSpan.FromSeconds(duration - 5);

        var query = EntityQueryEnumerator<NukeComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.MapUid != _station)
                continue;
            _sound.DispatchStationEventMusic(uid, _music, StationEventMusicType.Evac);
            _nuke = uid;

            var stationUid = _stationSystem.GetOwningStation(_nuke);

            if (stationUid != null)
                _alertLevelSystem.SetLevel(stationUid.Value, "nirvana", false, true, true, true);

            break;
        }

        var light_query = EntityQueryEnumerator<PoweredLightComponent, TransformComponent>();
        while (light_query.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.MapUid != _station)
                continue;
            if (HasComp<ExtensionCableReceiverComponent>(uid))
                RemComp<ExtensionCableReceiverComponent>(uid);
        }

        _chatSystem.DispatchGlobalAnnouncement($"Инициирован протокол экстренной эвакуации. Все активы признаны убыточными. Персоналу требуется немедленно пройти к эвакуационным капсулам. Капсулы будут запущены через {duration} секунд!", announcementSound: _announcement, colorOverride: Color.Crimson);
    }

    public void PreEvacuation()
    {
        _evac_state = EvacState.PreEvacuation;
        _chatSystem.DispatchGlobalAnnouncement("До запуска эвакуационных капсул осталось 5 секунд.", playSound: false, colorOverride: Color.Crimson);

        var query = EntityQueryEnumerator<EvacPodComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out var evac, out var xform))
        {
            _pointLight.SetEnabled(uid, true);
            _audio.PlayPvs(_audio.ResolveSound(evac.PreEvacSound), uid);
        }
    }

    public void EndEvacuation()
    {
        _evac_state = EvacState.Evacuated;
        _roundEnd.EndRound();

        if (_nuke != null)
            _sound.StopStationEventMusic(_nuke.Value, StationEventMusicType.Evac);

        var centcomm = _roundEnd.GetCentcomm();
        if (centcomm == null)
            return;

        var positions = GetEvacPositions(centcomm.Value);
        if (positions.Count == 0)
            return;

        var query = EntityQueryEnumerator<EvacPodComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out var evac, out var xform))
        {
            Spawn(evac.TeleportEffect, Transform(uid).Coordinates);

            _physics.SetBodyType(uid, BodyType.Dynamic);
            _pointLight.SetEnabled(uid, false);

            var targetCoords = positions[_random.Next(positions.Count)];
            _transformSystem.SetCoordinates(uid, targetCoords);
            Spawn(evac.TeleportEffect, targetCoords);
        }
    }

    private List<EntityCoordinates> GetEvacPositions(EntityUid mapUid)
    {
        var positions = new List<EntityCoordinates>();
        foreach (var (uid, transform) in EntityQuery<GasVentPumpComponent, TransformComponent>())
        {
            if (transform.MapUid != mapUid)
                continue;

            positions.Add(transform.Coordinates);
        }

        return positions;
    }

    private void OnRoundCleanup(RoundRestartCleanupEvent ev)
    {
        if (_nuke != null)
            _sound.StopStationEventMusic(_nuke.Value, StationEventMusicType.Evac);

        _station = null;
        _evac_state = EvacState.Idle;
        _evac_end = TimeSpan.Zero;
    }
}
