using Content.Server.Polymorph.Systems;
using Content.Server.Polymorph.Components;
using Content.Server.GridPreloader;
using Content.Shared.Atmos.Rotting;
using Content.Shared.Vanilla.Archon.Research;
using Content.Shared.Vanilla.Archon.OldMan;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs.Systems;
using Content.Shared.Vanilla.Damage.Events;
using Content.Shared.Damage.Events;
using Content.Shared.Damage.Components;
using Content.Shared.Damage;
using Content.Shared.Polymorph;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Administration;
using Content.Shared.Mobs;
using Content.Shared.Random;
using Content.Shared.Random.Helpers;
using Content.Shared.Mobs.Components;
using Content.Shared.Humanoid;
using Content.Shared.FixedPoint;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Actions;
using Content.Shared.Bed.Sleep;
using Content.Shared.Station;
using Content.Shared.Audio;
using Robust.Shared.Map.Components;
using Robust.Shared.Map;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Vanilla.Archon.OldMan;

public sealed partial class OldManSystem : SharedOldManSystem
{
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
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
        SubscribeLocalEvent<DimensionVictimComponent, MobStateChangedEvent>(OnVictimStateChanged);
        SubscribeLocalEvent<DimensionVictimComponent, MapInitEvent>(OnVictimInit);
        SubscribeLocalEvent<OldManPolymorphComponent, PolymorphedEvent>(OnPolyMorphRevert);
        SubscribeLocalEvent<OldManComponent, OldManTeleportEvent>(OnTeleportEvent);
        SubscribeLocalEvent<OldManComponent, PolymorphedEvent>(OnPolyMorph);
        SubscribeLocalEvent<OldManComponent, MeleeHitEvent>(OnMeleeHit);
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

            TeleportAnimation(target, false);
            var victim = EnsureComp<DimensionVictimComponent>(target);
            victim.OldMan = uid;
            victim.StationGridUid = comp.StationGridUid;
            victim.DimensionGridUid = comp.DimensionGridUid;
            for (int i = 0; i < victim.TeleportsAmount; i++)
            {
                if (TryGetRandomExistingTile(comp.DimensionGridUid, out var coords))
                    victim.Portals.Add(Spawn(victim.TeleportPrototype, coords.Value));
            }
            for (int i = 0; i < victim.FakeTeleportsAmount; i++)
            {
                if (TryGetRandomExistingTile(comp.DimensionGridUid, out var coords))
                    victim.Portals.Add(Spawn(victim.FakeTeleportPrototype, coords.Value));
            }

        }
    }
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _updateDif += frameTime;
        if (_updateDif < UpdateRate)
            return;
        _updateDif -= UpdateRate;
        var now = timing.CurTime;

        var query = EntityQueryEnumerator<DimensionVictimComponent>();
        while (query.MoveNext(out var uid, out var comp))
            DamageVictim(uid, comp, now);
        var animQuery = EntityQueryEnumerator<PDAnimationComponent>();
        while (animQuery.MoveNext(out var uid, out var comp))
            ProcessTeleport(uid, comp, now);
    }

    #region старик
    protected void OnTeleportEvent(EntityUid uid, OldManComponent comp, OldManTeleportEvent args)
    {
        if (args.Handled)
            return;

        if (Transform(uid).GridUid == null)
            return;
        audio.PlayPvs(comp.TeleportSound, uid);
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
            trans.SetCoordinates(uid, comp.FallBackCoords.Value);
        TeleportAnimation(uid, true);
        audio.PlayPvs(comp.TeleportSound, uid);
        comp.PolyMorphEntity = null;
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
            comp.DimensionUid = mapSystem.CreateMap(out _, runMapInit: false);
            trans.SetParent(comp.DimensionGridUid, comp.DimensionUid);
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

        mapSystem.InitializeMap(comp.DimensionUid);

        comp.ActionEnt = _actions.AddAction(uid, comp.ActionId);
        audio.PlayPvs(comp.MapInitSound, uid);
        TeleportAnimation(uid, true);
    }
    private void OnComponentShutdown(EntityUid uid, OldManComponent comp, ref ComponentShutdown args)
    {
        ReturnAllVictims(uid);
        QueueDel(comp.DimensionUid);
    }
    private void OnPolyMorph(EntityUid uid, OldManComponent comp, ref PolymorphedEvent args)
    {
        comp.PolyMorphEntity = args.NewEntity;
    }
    protected override void RevertPolymorph(EntityUid uid)
    {
        if (!TryComp<OldManComponent>(uid, out var comp))
            return;
        if (comp.PolyMorphEntity == null)
            return;
        if (!TryComp<PolymorphedEntityComponent>(comp.PolyMorphEntity, out var polyComp))
            return;
        _polymorph.Revert((comp.PolyMorphEntity.Value, polyComp));
    }

    #endregion
}
