using Content.Server.Ghost.Roles;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Polymorph.Systems;
using Content.Shared.Polymorph;
using Content.Shared.Vanilla.Archon.Research;
using Content.Shared.Vanilla.Archon.OldMan;
using Content.Shared.Vanilla.Damage.Events;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Damage;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Overlays;
using Content.Shared.Administration;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Humanoid;
using Content.Shared.FixedPoint;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Jittering;
using Content.Shared.Damage.Events;
using Content.Shared.Actions;
using Content.Shared.Bed.Sleep;
using Content.Shared.Random;
using Content.Shared.Random.Helpers;
using Content.Shared.Station;
using Robust.Shared.Physics.Events;
using Robust.Shared.Map.Components;
using Robust.Shared.Map;
using Robust.Shared.Timing;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Random;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks.Dataflow;
using System.Linq;

namespace Content.Server.Vanilla.Archon.OldMan;

public sealed class OldManSystem : EntitySystem
{
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _trans = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedJitteringSystem _jitter = default!;
    [Dependency] private readonly DamageableSystem _damageableSystem = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedStationSystem _station = default!;
    [Dependency] private readonly PolymorphSystem _polymorph = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    private const float UpdateRate = 0.25f;
    private float _updateDif;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<OldManComponent, ResearchAttemptEvent>(OnResearchAttempt);
        SubscribeLocalEvent<OldManComponent, ComponentShutdown>(OnComponentShutdown);
        SubscribeLocalEvent<OldManComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<OldManPolymorphComponent, PolymorphedEvent>(OnPolyMorph);
        SubscribeLocalEvent<OldManComponent, OldManTeleportEvent>(OnTeleportEvent);
        SubscribeLocalEvent<OldManComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<OldManComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<DimensionVictimComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMoveSpeed);
        SubscribeLocalEvent<DimensionVictimComponent, MapInitEvent>(OnVictimInit);
        SubscribeLocalEvent<DimensionVictimComponent, MobStateChangedEvent>(OnVictimStateChanged);
        SubscribeLocalEvent<DimensionEscapeTeleportComponent, StartCollideEvent>(OnCollide);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _updateDif += frameTime;
        if (_updateDif < UpdateRate)
            return;

        _updateDif -= UpdateRate;
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<OldManComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var trans))
            ProcessTeleport(uid, comp, trans, now);

        var victimQuery = EntityQueryEnumerator<DimensionVictimComponent>();
        while (victimQuery.MoveNext(out var uid, out var comp))
            DamageVictim(uid, comp, now);
    }

    #region старик
    private void OnResearchAttempt(EntityUid uid, OldManComponent comp, ResearchAttemptEvent args)
    {
        if (!HasComp<SleepingComponent>(uid))
            args.Cancel();
    }

    private void OnMobStateChanged(EntityUid uid, OldManComponent comp, MobStateChangedEvent args)
    {
        if (args.OldMobState > args.NewMobState)
            return;
        //возвращаем всех на станцию
        ReturnAllVictims((uid, comp));

        //отмена тп при смерти
        if (args.NewMobState == MobState.Dead)
        {
            comp.TPState = TeleportState.NoTP;
            _appearance.SetData(uid, DamageVisualizerKeys.DamageUpdateGroups, comp.TPState);
            RemComp<AdminFrozenComponent>(uid);
        }
    }

    private void OnComponentShutdown(EntityUid uid, OldManComponent comp, ref ComponentShutdown args)
    {
        ReturnAllVictims((uid, comp));
        if (!Deleted(comp.DimensionUid))
            QueueDel(comp.DimensionUid);
    }

    private void ProcessTeleport(EntityUid uid, OldManComponent comp, TransformComponent trans, TimeSpan now)
    {
        //вошли в телепорт
        if (comp.TPState == TeleportState.In && now >= comp.TeleportationInEndAt)
        {
            comp.TPState = TeleportState.Out;
            _appearance.SetData(uid, DamageVisualizerKeys.DamageUpdateGroups, comp.TPState);
            _polymorph.PolymorphEntity(uid, "OldManJaunt");//туду компонент
        }
        //вышли из телепорта
        if (comp.TPState == TeleportState.Out && now >= comp.TeleportationOutEndAt)
        {
            comp.TPState = TeleportState.NoTP;
            _appearance.SetData(uid, DamageVisualizerKeys.DamageUpdateGroups, comp.TPState);
            RemComp<AdminFrozenComponent>(uid);
        }
    }

    private void OnPolyMorph(Entity<OldManPolymorphComponent> uid, ref PolymorphedEvent args)
    {
        if (!args.IsRevert)
            return;

        if (!TryComp<OldManComponent>(args.NewEntity, out var comp))
            return;

        comp.TPState = TeleportState.Out;
        comp.TeleportationOutEndAt = _timing.CurTime + comp.TeleportOutDuration;
        _appearance.SetData(args.NewEntity, DamageVisualizerKeys.DamageUpdateGroups, comp.TPState);
        EnsureComp<AdminFrozenComponent>(args.NewEntity);
    }

    public bool TryGetRandomExistingTile(EntityUid gridUid, [NotNullWhen(true)] out EntityCoordinates? coords)
    {
        coords = null;
        if (!Exists(gridUid) || Deleted(gridUid))
            return false;

        if (!TryComp<MapGridComponent>(gridUid, out var grid))
            return false;

        var tiles = _mapSystem.GetAllTiles(gridUid, grid).ToList();
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

    private void OnMeleeHit(EntityUid uid, OldManComponent comp, ref MeleeHitEvent args)
    {
        if (!args.IsHit)
            return;

        if (!TryGetRandomExistingTile(comp.DimensionGridUid, out var coords))
            coords = Transform(comp.DimensionUid).Coordinates;

        foreach (var target in args.HitEntities)
        {
            if (!TryComp<MobStateComponent>(target, out var mob))
                continue;
            if (mob.CurrentState == MobState.Dead)
                continue;

            _trans.SetCoordinates(target, coords.Value);
            var victim = EnsureComp<DimensionVictimComponent>(target);
            victim.OldMan = (uid, comp);
            EnsureComp<NoirOverlayComponent>(target);
        }
    }

    private void OnTeleportEvent(EntityUid uid, OldManComponent comp, OldManTeleportEvent args)
    {
        if (args.Handled)
            return;

        if (comp.TPState != TeleportState.NoTP)
            return;

        if (Transform(uid).GridUid == null)
            return;

        EnsureComp<AdminFrozenComponent>(uid);
        _appearance.SetData(uid, DamageVisualizerKeys.DamageUpdateGroups, TeleportState.In);
        _audio.PlayPvs(comp.TeleportSound, uid);
        comp.TPState = TeleportState.In;
        comp.TeleportationInEndAt = _timing.CurTime + comp.TeleportInDuration;
        args.Handled = true;
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

        _mapSystem.InitializeMap(dimension.Value.Comp.MapId);

        comp.DimensionGridUid = largestGrid.Value;
        comp.ActionEnt = _actions.AddAction(uid, comp.ActionId);
    }


    #endregion
    #region измерение и жертвы
    private void DamageVictim(EntityUid uid, DimensionVictimComponent comp, TimeSpan now)
    {
        if (now >= comp.NextDamage)
        {
            comp.NextDamage = now + comp.DamageInterval;

            _audio.PlayPvs(comp.DamageSound, uid);
            _popup.PopupEntity("Кожа гниёт на глазах", uid, PopupType.SmallCaution);//туду в фтл
            _damageableSystem.TryChangeDamage(uid, comp.Damage);
        }
    }

    private void OnVictimStateChanged(EntityUid uid, DimensionVictimComponent component, MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Critical)
            return;

        var deadResult = _proto.Index<WeightedRandomPrototype>(component.DeadResults).Pick(_random);
        switch (deadResult)
        {
            case "Kill":
                _mobState.ChangeMobState(uid, MobState.Dead, origin: component.OldMan);
                ReturnVictimOnStation(uid, component);
                break;
            case "Revive":
                _popup.PopupEntity("П О Д Н И М А Й С Я", uid, PopupType.SmallCaution);//туду в фтл
                if (TryComp<DamageableComponent>(uid, out var damagComp))
                    _damageableSystem.SetAllDamage((uid, damagComp), 0);
                break;
            case "Eat":
                //persih сгнить
                //деда в слип
                var sleep = EnsureComp<SleepingComponent>(component.OldMan.Owner);
                sleep.WakeThreshold = FixedPoint2.New(3);
                sleep.CooldownEnd = _timing.CurTime + TimeSpan.FromMinutes(120);
                _mobState.ChangeMobState(uid, MobState.Dead, origin: component.OldMan);
                ReturnVictimOnStation(uid, component);
                break;

        }
    }

    private void OnRefreshMoveSpeed(EntityUid uid, DimensionVictimComponent component, RefreshMovementSpeedModifiersEvent args)
    {
        args.ModifySpeed(0.5f, 0.5f);
    }

    private void OnVictimInit(EntityUid uid, DimensionVictimComponent comp, ref MapInitEvent args)
    {
        comp.NextDamage = _timing.CurTime + comp.DamageInterval;

        var grid = Transform(uid).GridUid;
        if (grid == null)
        {
            RemComp<DimensionVictimComponent>(uid);
            return;
        }

        comp.DimensionGridUid = grid.Value;
        _jitter.AddJitter(uid, 2, 2);
        comp.Stream = _audio.PlayGlobal(comp.DimensionAmbient, uid)?.Entity;

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
    //в шаред
    private void OnCollide(EntityUid uid, DimensionEscapeTeleportComponent comp, ref StartCollideEvent args)
    {
        if (!TryComp<DimensionVictimComponent>(args.OtherEntity, out var victim))
            return;

        QueueDel(uid);

        if (comp.IsFake)
        {
            _audio.PlayGlobal(victim.DimensionEscapeSound, args.OtherEntity);
            return;
        }

        ReturnVictimOnStation(args.OtherEntity, victim);
    }

    private void ReturnVictimOnStation(EntityUid uid, DimensionVictimComponent comp)
    {
        void TP(EntityCoordinates targetCoords)
        {
            _trans.SetCoordinates(uid, targetCoords);
            RemComp<DimensionVictimComponent>(uid);
            RemComp<NoirOverlayComponent>(uid);
            RemCompDeferred<JitteringComponent>(uid);
            _audio.PlayPvs(comp.DimensionEscapeSound, uid);
            comp.Stream = _audio.Stop(comp.Stream);
            foreach (var portal in comp.Portals)
            {
                if (Exists(portal) && !Deleted(portal))
                    QueueDel(portal);
            }
        }

        var grid = comp.OldMan.Comp.StationGridUid;
        if (!Exists(grid) || Deleted(grid))
            return;

        //1. тпшимся к другому игроку
        var query = EntityQueryEnumerator<TransformComponent, HumanoidProfileComponent>();
        while (query.MoveNext(out var target, out var trans, out _))
        {
            //Должен быть на гриде где дедушка уходил в карманное измерение последний раз
            if (trans.GridUid != grid)
                continue;

            TP(Transform(target).Coordinates);
            _popup.PopupEntity($"{Name(uid)} падает с потолка", uid, PopupType.LargeCaution);//туду в фтл
            return;
        }

        //2. Если не получилось, то просто тпшимся на грид с которого уходили
        if (TryGetRandomExistingTile(grid, out var coords))
            TP(coords.Value);
    }
    /// <summary>
    /// возврат всех жертв на станцию
    /// </summary>
    private void ReturnAllVictims(Entity<OldManComponent> OldMan)
    {
        var victimQuery = EntityQueryEnumerator<DimensionVictimComponent>();
        while (victimQuery.MoveNext(out var uid, out var comp))
        {
            if (comp.OldMan == OldMan)
                ReturnVictimOnStation(uid, comp);
        }

    }
    #endregion
}
