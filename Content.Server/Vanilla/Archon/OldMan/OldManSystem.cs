using Content.Server.Polymorph.Systems;
using Content.Server.Polymorph.Components;
using Content.Server.GridPreloader;
using Content.Shared.Atmos.Rotting;
using Content.Shared.Vanilla.Archon.Research;
using Content.Shared.Vanilla.Archon.OldMan;
using Content.Shared.Vanilla.Damage.Events;
using Content.Shared.Damage.Events;
using Content.Shared.Damage.Components;
using Content.Shared.Damage;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Overlays;
using Content.Shared.Administration;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Humanoid;
using Content.Shared.FixedPoint;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Jittering;
using Content.Shared.Actions;
using Content.Shared.Bed.Sleep;
using Content.Shared.Station;
using Robust.Shared.Map.Components;
using Robust.Shared.Map;
using Robust.Shared.EntitySerialization.Systems;
using System.Threading.Tasks.Dataflow;

namespace Content.Server.Vanilla.Archon.OldMan;

public sealed partial class OldManSystem : SharedOldManSystem
{
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly SharedJitteringSystem _jitter = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedStationSystem _station = default!;
    [Dependency] private readonly PolymorphSystem _polymorph = default!;
    [Dependency] private readonly GridPreloaderSystem _preload = default!;


    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<OldManComponent, ResearchAttemptEvent>(OnResearchAttempt);
        SubscribeLocalEvent<OldManComponent, ComponentShutdown>(OnComponentShutdown);
        SubscribeLocalEvent<OldManComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<DimensionVictimComponent, MapInitEvent>(OnVictimInit);
    }

    protected override void Update(TimeSpan now)
    {
        var victimQuery = EntityQueryEnumerator<DimensionVictimComponent>();
        while (victimQuery.MoveNext(out var uid, out var comp))
            DamageVictim(uid, comp, now);
        base.Update(now);
    }

    #region старик
    protected override void ProcessTeleport(EntityUid uid, OldManComponent comp, TimeSpan now)
    {
        //вошли в телепорт
        if (comp.TPState == TeleportState.In && now >= comp.TeleportationInEndAt)
        {
            comp.TPState = TeleportState.Out;
            appearance.SetData(uid, DamageVisualizerKeys.DamageUpdateGroups, comp.TPState);
            _polymorph.PolymorphEntity(uid, "OldManJaunt");
            var xform = Transform(uid);

            if (xform.GridUid is not { } previousGrid)
                return;

            comp.FallBackCoords = xform.Coordinates;
            comp.PreviousGrid = previousGrid;
        }
        //вышли из телепорта
        if (comp.TPState == TeleportState.Out && now >= comp.TeleportationOutEndAt)
        {
            comp.TPState = TeleportState.NoTP;
            appearance.SetData(uid, DamageVisualizerKeys.DamageUpdateGroups, comp.TPState);
            RemComp<AdminFrozenComponent>(uid);
        }
    }
    private void OnResearchAttempt(EntityUid uid, OldManComponent comp, ResearchAttemptEvent args)
    {
        if (!HasComp<SleepingComponent>(uid))
            args.Cancel();
    }

    private void OnComponentShutdown(EntityUid uid, OldManComponent comp, ref ComponentShutdown args)
    {
        ReturnAllVictims((uid, comp));
        QueueDel(comp.DimensionUid);
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

        comp.TPState = TeleportState.Out;
        comp.TeleportationOutEndAt = timing.CurTime + comp.TeleportOutDuration;
        appearance.SetData(uid, DamageVisualizerKeys.DamageUpdateGroups, comp.TPState);
        EnsureComp<AdminFrozenComponent>(uid);
    }

    protected override void RevertPolymorph(OldManComponent comp)
    {
        if (comp.PolyMorphEntity == null)
            return;

        if (!TryComp<PolymorphedEntityComponent>(comp.PolyMorphEntity, out var polyComp))
            return;

        _polymorph.Revert((comp.PolyMorphEntity, polyComp));
    }

    #endregion
    #region измерение и жертвы
    private void DamageVictim(EntityUid uid, DimensionVictimComponent comp, TimeSpan now)
    {
        if (now >= comp.NextDamage)
        {
            comp.NextDamage = now + comp.DamageInterval;
            audio.PlayPvs(comp.DamageSound, uid);
            damageable.TryChangeDamage(uid, comp.Damage);
        }
    }

    private void OnVictimInit(EntityUid uid, DimensionVictimComponent comp, ref MapInitEvent args)
    {
        comp.NextDamage = timing.CurTime + comp.DamageInterval;

        var grid = Transform(uid).GridUid;
        if (grid == null)
        {
            RemComp<DimensionVictimComponent>(uid);
            return;
        }
        comp.DimensionGridUid = grid.Value;
        _jitter.AddJitter(uid, 2, 2);
        comp.Stream = audio.PlayGlobal(comp.DimensionAmbient, uid)?.Entity;

        for (int i = 0; i < comp.TeleportsAmount; i++)
        {
            if (TryGetRandomExistingTile(grid.Value, out var coords))
                comp.Portals.Add(Spawn(comp.TeleportPrototype, coords.Value));
        }

        for (int i = 0; i < comp.FakeTeleportsAmount; i++)
        {
            if (TryGetRandomExistingTile(grid.Value, out var coords))
                comp.Portals.Add(Spawn(comp.FakeTeleportPrototype, coords.Value));
        }
    }
    #endregion
}
