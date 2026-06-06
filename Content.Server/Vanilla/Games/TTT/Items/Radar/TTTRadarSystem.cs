using Content.Shared.Mobs.Components;
using Content.Shared.Pinpointer;
using Content.Shared.Mobs;
using Content.Shared.Vanilla.Games.TTT;
using Content.Shared.Vanilla.Games.TTT.Items.Radar;
using Content.Shared.Vanilla.Games.TTT.Items.DNAScanner;
using Robust.Server.GameObjects;

using Robust.Shared.Timing;
namespace Content.Server.Vanilla.Games.TTT.Items.Radar;

public sealed partial class TTTRadarSystem : EntitySystem
{
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private IGameTiming _timing = default!;
    private float _accumulator = 0f;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TTTRadarComponent, MapInitEvent>(OnMapInit);
        Subs.BuiEvents<TTTRadarComponent>(StationMapUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnRadarOpened);
        });
    }
    private void OnRadarOpened(EntityUid uid, TTTRadarComponent component, BoundUIOpenedEvent args)
    {
        if (HasComp<TTTTRAITORComponent>(args.Actor))
            component.TraitorRadar = true;
    }
    private void OnMapInit(EntityUid uid, TTTRadarComponent radar, ref MapInitEvent args)
    {
        var query = EntityQueryEnumerator<TTTMarkerComponent, TransformComponent, MobStateComponent>();
        while (query.MoveNext(out _, out var marker, out var xform, out var mobState))
        {
            if (mobState.CurrentState != MobState.Alive)
                continue;

            var coords = GetNetCoordinates(xform.Coordinates);
            var color = marker.GetColor();
            if (!radar.TraitorRadar && marker.Role == TTTRole.Traitor)
                color = Color.Green;
            radar.TrackedEntities.Add(new RadarBlip(coords, color));
        }

        var decoyQuery = EntityQueryEnumerator<TTTDecoyComponent, TransformComponent>();
        while (decoyQuery.MoveNext(out _, out var decoy, out var xform))
        {
            var coords = GetNetCoordinates(xform.Coordinates);
            var color = radar.TraitorRadar ? Color.Gray : Color.Green;
            radar.TrackedEntities.Add(new RadarBlip(coords, color));
        }
        var remaining = TimeSpan.FromSeconds(30f - _accumulator);
        radar.NextScan = _timing.CurTime + remaining;
        Dirty(uid, radar);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _accumulator += frameTime;
        if (_accumulator < 30f)
            return;

        _accumulator = 0f;

        var radarQuery = EntityQueryEnumerator<TTTRadarComponent>();
        while (radarQuery.MoveNext(out var uid, out var radar))
        {
            radar.TrackedEntities.Clear();
            var query = EntityQueryEnumerator<TTTMarkerComponent, TransformComponent, MobStateComponent>();
            while (query.MoveNext(out _, out var marker, out var xform, out var mobState))
            {
                if (mobState.CurrentState != MobState.Alive)
                    continue;

                var coords = GetNetCoordinates(xform.Coordinates);
                var color = marker.GetColor();
                if (!radar.TraitorRadar && marker.Role == TTTRole.Traitor)
                    color = Color.Green;

                radar.TrackedEntities.Add(new RadarBlip(coords, color));
            }
            var decoyQuery = EntityQueryEnumerator<TTTDecoyComponent, TransformComponent>();
            while (decoyQuery.MoveNext(out _, out var decoy, out var xform))
            {
                var coords = GetNetCoordinates(xform.Coordinates);
                var color = radar.TraitorRadar ? Color.Gray : Color.Green;
                radar.TrackedEntities.Add(new RadarBlip(coords, color));
            }

            var remaining = TimeSpan.FromSeconds(30f - _accumulator);
            radar.NextScan = _timing.CurTime + remaining;
            Dirty(uid, radar);
            _ui.SetUiState(uid, StationMapUiKey.Key, new TTTRadarInterfaceState());
        }
    }
}
