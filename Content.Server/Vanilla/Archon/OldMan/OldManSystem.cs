using Content.Server.Polymorph.Systems;
using Content.Server.Polymorph.Components;
using Content.Server.GridPreloader;
using Content.Shared.Vanilla.Archon.Research;
using Content.Shared.Vanilla.Archon.OldMan;
using Content.Shared.Polymorph;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Bed.Sleep;
using Content.Shared.Audio;
using Robust.Shared.EntitySerialization.Systems;
using Content.Shared.Station;

namespace Content.Server.Vanilla.Archon.OldMan;

public sealed partial class OldManSystem : SharedOldManSystem
{
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;

    [Dependency] private readonly SharedStationSystem _station = default!;
    [Dependency] private readonly PolymorphSystem _polymorph = default!;
    [Dependency] private readonly GridPreloaderSystem _preload = default!;
    [Dependency] private readonly SharedAmbientSoundSystem _ambient = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<OldManComponent, ResearchAttemptEvent>(OnResearchAttempt);
        SubscribeLocalEvent<OldManComponent, ComponentShutdown>(OnComponentShutdown);
        SubscribeLocalEvent<OldManComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<OldManComponent, SleepStateChangedEvent>(OnSLeep);
        SubscribeLocalEvent<OldManComponent, OldManTeleportEvent>(OnTeleportEvent);
        SubscribeLocalEvent<OldManComponent, PolymorphedEvent>(OnPolyMorph);
        SubscribeLocalEvent<OldManComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<OldManPolymorphComponent, PolymorphedEvent>(OnPolyMorphRevert);
        SubscribeLocalEvent<DimensionVictimComponent, MobStateChangedEvent>(OnVictimStateChanged);
        SubscribeLocalEvent<DimensionVictimComponent, MapInitEvent>(OnVictimInit);
    }


    private void OnMeleeHit(EntityUid uid, OldManComponent comp, ref MeleeHitEvent args)
    {
        if (!args.IsHit)
            return;

        foreach (var target in args.HitEntities)
        {
            if (!TryComp<MobStateComponent>(target, out var mob))
                continue;

            if (mob.CurrentState == MobState.Dead)
                continue;

            if (HasComp<PDAnimationComponent>(target))
                continue;
            EatVictim(target, uid);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        UpdateDif += frameTime;
        if (UpdateDif < UpdateRate)
            return;
        UpdateDif -= UpdateRate;
        var now = Timing.CurTime;

        var query = EntityQueryEnumerator<DimensionVictimComponent>();
        while (query.MoveNext(out var uid, out var comp))
            DamageVictim(uid, comp, now);
        var animQuery = EntityQueryEnumerator<PDAnimationComponent>();
        while (animQuery.MoveNext(out var uid, out var comp))
            ProcessTeleport(uid, comp, now);
    }

    #region старик
    private void OnTeleportEvent(EntityUid uid, OldManComponent comp, OldManTeleportEvent args)
    {
        if (args.Handled)
            return;

        if (Transform(uid).GridUid == null)
            return;
        Audio.PlayPvs(comp.TeleportSound, uid);
        TeleportAnimation(uid, false);
        args.Handled = true;
    }
    private void OnPolyMorphRevert(Entity<OldManPolymorphComponent> ent, ref PolymorphedEvent args)
    {
        if (!args.IsRevert)
            return;
        var uid = args.NewEntity;
        if (!TryComp<OldManComponent>(uid, out var comp))
            return;
        var oldmanTrans = Transform(uid);
        if ((oldmanTrans.GridUid == null || oldmanTrans.GridUid != comp.PreviousGrid) && comp.FallBackCoords != null)
            Trans.SetCoordinates(uid, comp.FallBackCoords.Value);
        TeleportAnimation(uid, true);
        Audio.PlayPvs(comp.TeleportSound, uid);
    }

    private void OnSLeep(EntityUid uid, OldManComponent comp, ref SleepStateChangedEvent args)
    {
        _ambient.SetAmbience(uid, !args.FellAsleep);
        ReturnAllVictims(uid);
    }

    private void OnResearchAttempt(EntityUid uid, OldManComponent comp, ResearchAttemptEvent args)
    {
        if (!HasComp<SleepingComponent>(uid))
            args.Cancel();
    }


    private void OnMapInit(EntityUid uid, OldManComponent comp, ref MapInitEvent args)
    {
        // ---- Станция ----
        if (_station.GetOwningStation(uid) is not { } station)
        {
            Log.Error($"не удалось найти станцию у старика {uid}");
            QueueDel(uid);
            return;
        }

        if (_station.GetLargestGrid(station) is not { } largestStationGrid)
        {
            Log.Error($"не удалось найти грид станции у старика {uid}");
            QueueDel(uid);
            return;
        }

        comp.StationGridUid = largestStationGrid;

        // ---- Загрузка измерения ----
        if (_preload.TryGetPreloadedGrid(comp.PreLoadGridProto, out var gridUid))
        {
            comp.DimensionGridUid = gridUid.Value;
            comp.DimensionUid = MapSystem.CreateMap(out _, runMapInit: false);
            Trans.SetParent(comp.DimensionGridUid, comp.DimensionUid);
        }
        else
        {
            //если не удалось предзагрузить, то создаем новую карту
            Log.Warning($"не удалось предзагрузить карту");
            if (!_mapLoader.TryLoadMap(comp.DimensionMap, out var dimension, out var grids))
            {
                Log.Error($"не удалось загрузить карту при создании старика {uid}");
                QueueDel(uid);
                return;
            }

            comp.DimensionUid = dimension.Value.Owner;

            EntityUid? largestGrid = null;
            Box2 largestBounds = new Box2();

            foreach (var grid in grids)
            {
                if (grid.Comp.LocalAABB.Size.LengthSquared() < largestBounds.Size.LengthSquared())
                    continue;

                largestBounds = grid.Comp.LocalAABB;
                largestGrid = grid.Owner;
            }

            if (largestGrid == null)
            {
                Log.Error($"не удалось найти грид при создании старика {uid}");
                QueueDel(uid);
                return;
            }
            comp.DimensionGridUid = largestGrid.Value;
        }

        MapSystem.InitializeMap(comp.DimensionUid);

        comp.ActionEnt = Actions.AddAction(uid, comp.ActionId);
        Audio.PlayPvs(comp.MapInitSound, uid);
        TeleportAnimation(uid, true);
    }
    private void OnComponentShutdown(EntityUid uid, OldManComponent comp, ref ComponentShutdown args)
    {
        ReturnAllVictims(uid);
        QueueDel(comp.DimensionUid);
    }
    private void OnPolyMorph(EntityUid uid, OldManComponent comp, ref PolymorphedEvent args)
    {
        if (TryComp<OldManPolymorphComponent>(args.NewEntity, out var polyComp))
        {
            polyComp.OldMan = uid;
            polyComp.StationGridUid = comp.StationGridUid;
        }

    }
    public override void RevertPolymorph(EntityUid uid)
    {
        var query = EntityQueryEnumerator<OldManPolymorphComponent>();
        while (query.MoveNext(out var polyUid, out var polyComp))
        {
            if (uid == polyComp.OldMan)
                _polymorph.Revert(polyUid);
        }
    }

    #endregion
}
