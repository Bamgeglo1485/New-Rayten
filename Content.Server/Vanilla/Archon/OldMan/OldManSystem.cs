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
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Overlays;
using Content.Shared.Administration;
using Content.Shared.Mobs;
using Content.Shared.Random;
using Content.Shared.Random.Helpers;
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
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
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
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly MobStateSystem _mobstateSystem = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] protected readonly IRobustRandom _random = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<OldManComponent, ResearchAttemptEvent>(OnResearchAttempt);
        SubscribeLocalEvent<OldManComponent, ComponentShutdown>(OnComponentShutdown);
        SubscribeLocalEvent<OldManComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<DimensionVictimComponent, MapInitEvent>(OnVictimInit);
        SubscribeLocalEvent<DimensionVictimComponent, MobStateChangedEvent>(OnVictimStateChanged);
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
            var xform = Transform(uid);

            if (xform.GridUid is not { } previousGrid)
                return;

            comp.FallBackCoords = xform.Coordinates;
            comp.PreviousGrid = previousGrid;

            _polymorph.PolymorphEntity(uid, "OldManJaunt");
            Dirty(uid, comp);
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
    private void OnComponentShutdown(EntityUid uid, OldManComponent comp, ref ComponentShutdown args)
    {
        ReturnAllVictims(uid);
        QueueDel(comp.DimensionUid);
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
    #region измерение и жертвы
    /// <summary>
    /// возвращает конкретного персонажа на конкретные координат
    /// </summary>
    private void ReturnVictimOnCoords(EntityUid uid, DimensionVictimComponent comp, EntityCoordinates targetCoords)
    {
        trans.SetCoordinates(uid, targetCoords);
        RemComp<DimensionVictimComponent>(uid);
        audio.PlayPvs(comp.DimensionEscapeSound, uid);
    }
    private void OnVictimStateChanged(EntityUid uid, DimensionVictimComponent component, MobStateChangedEvent args)
    {
        if (args.OldMobState != MobState.Alive)
            return;

        var deadResult = _proto.Index<WeightedRandomPrototype>(component.DeadResults).Pick(_random);
        switch (deadResult)
        {
            case "Kill":
                _mobstateSystem.ChangeMobState(uid, MobState.Dead, origin: component.OldMan);
                ReturnVictimOnStation(uid, component);
                break;
            case "Revive":
                _popup.PopupEntity("П О Д Н И М А Й С Я", uid, PopupType.LargeCaution);//туду в фтл
                if (TryComp<DamageableComponent>(uid, out var damagComp))
                    _damageable.SetAllDamage((uid, damagComp), 0);
                _mobstateSystem.ChangeMobState(uid, MobState.Alive, origin: component.OldMan);
                break;
            case "Eat":
                var sleep = EnsureComp<SleepingComponent>(component.OldMan);
                sleep.WakeThreshold = FixedPoint2.New(3);
                sleep.CooldownEnd = timing.CurTime + TimeSpan.FromMinutes(120);
                _mobstateSystem.ChangeMobState(uid, MobState.Dead, origin: component.OldMan);
                RevertPolymorph(component.OldMan);
                ReturnVictimOnStation(uid, component);
                if (TryComp<PerishableComponent>(uid, out var perish))
                    perish.RotAccumulator = perish.RotAfter;
                break;
        }
    }
    private void DamageVictim(EntityUid uid, DimensionVictimComponent comp, TimeSpan now)
    {
        if (now < comp.NextDamage)
            return;

        comp.NextDamage = now + comp.DamageInterval;
        audio.PlayPvs(comp.DamageSound, uid);
        _damageable.TryChangeDamage(uid, comp.Damage);
    }

    private void OnVictimInit(EntityUid uid, DimensionVictimComponent comp, ref MapInitEvent args)
    {
        comp.NextDamage = timing.CurTime + comp.DamageInterval;

        var grid = Transform(uid).GridUid;
        if (grid == null || !_mobstateSystem.IsAlive(uid))
        {
            ReturnVictimOnStation(uid, comp);
            return;
        }
        EnsureComp<NoirOverlayComponent>(uid);
        audio.PlayPvs(comp.DimensionEnterSound, uid);
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
    public override void ReturnAllVictims(EntityUid oldMan)
    {
        var victimQuery = EntityQueryEnumerator<DimensionVictimComponent>();
        while (victimQuery.MoveNext(out var uid, out var comp))
        {
            if (comp.OldMan == oldMan) ReturnVictimOnCoords(uid, comp, Transform(oldMan).Coordinates.Offset(_random.NextVector2(1f)));
        }
    }
    public override void ReturnVictimOnStation(EntityUid uid, DimensionVictimComponent comp)
    {
        var grid = comp.StationGridUid;
        if (!Exists(grid) || Deleted(grid))
            return;

        //1. тпшимся к другому игроку
        var query = EntityQueryEnumerator<TransformComponent, HumanoidProfileComponent>();
        while (query.MoveNext(out var target, out var trans, out _))
        {
            //Должен быть на гриде где дедушка уходил в карманное измерение последний раз
            if (trans.GridUid != grid)
                continue;

            ReturnVictimOnCoords(uid, comp, Transform(target).Coordinates);
            return;
        }

        //2. Если не получилось, то просто тпшимся на грид с которого уходили
        if (TryGetRandomExistingTile(grid, out var coords))
            ReturnVictimOnCoords(uid, comp, coords.Value);
    }
    public bool TryGetRandomExistingTile(EntityUid gridUid, [NotNullWhen(true)] out EntityCoordinates? coords)
    {
        coords = null;
        if (!Exists(gridUid) || Deleted(gridUid))
            return false;

        if (!TryComp<MapGridComponent>(gridUid, out var grid))
            return false;

        var tiles = mapSystem.GetAllTiles(gridUid, grid).ToList();
        _random.Shuffle(tiles);
        foreach (var tile in tiles)
        {
            if (_turf.IsTileBlocked(tile, CollisionGroup.MobMask))
                continue;

            coords = new EntityCoordinates(gridUid, tile.GridIndices);
            return true;
        }
        return false;
    }
    #endregion
}
