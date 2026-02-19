using Content.Shared.Audio;
using Content.Shared.Administration;
using Content.Shared.Atmos.Rotting;
using Content.Shared.Bed.Sleep;
using Content.Shared.Movement.Systems;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Polymorph;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Maps;
using Content.Shared.Random;
using Content.Shared.Random.Helpers;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Damage.Components;
using Content.Shared.Physics;
using Content.Shared.Overlays;
using Content.Shared.Jittering;
using Content.Shared.Humanoid;
using Content.Shared.FixedPoint;
using Robust.Shared.Map.Components;
using Robust.Shared.Map;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics.Events;
using Robust.Shared.Random;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Content.Shared.Vanilla.Archon.OldMan;

public abstract class SharedOldManSystem : EntitySystem
{
    [Dependency] protected readonly SharedAppearanceSystem appearance = default!;
    [Dependency] protected readonly SharedAudioSystem audio = default!;
    [Dependency] protected readonly SharedPopupSystem popup = default!;
    [Dependency] protected readonly SharedTransformSystem trans = default!;
    [Dependency] protected readonly SharedMapSystem mapSystem = default!;
    [Dependency] protected readonly DamageableSystem damageable = default!;
    [Dependency] protected readonly IGameTiming timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedAmbientSoundSystem _ambient = default!;
    protected const float UpdateRate = 0.1f;
    protected float _updateDif;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<OldManComponent, SleepStateChangedEvent>(OnSLeep);
        SubscribeLocalEvent<DimensionVictimComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMoveSpeed);
        SubscribeLocalEvent<OldManComponent, PolymorphedEvent>(OnPolyMorph);
        SubscribeLocalEvent<OldManPolymorphComponent, PolymorphedEvent>(OnPolyMorphRevert);
        SubscribeLocalEvent<DimensionEscapeTeleportComponent, StartCollideEvent>(OnCollide);
        SubscribeLocalEvent<DimensionVictimComponent, MobStateChangedEvent>(OnVictimStateChanged);
        SubscribeLocalEvent<OldManComponent, OldManTeleportEvent>(OnTeleportEvent);
        SubscribeLocalEvent<OldManComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<OldManComponent, MeleeHitEvent>(OnMeleeHit);
    }
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _updateDif += frameTime;
        if (_updateDif < UpdateRate)
            return;

        _updateDif -= UpdateRate;

        var now = timing.CurTime;
        Update(now);
    }
    protected virtual void Update(TimeSpan now)
    {
        var query = EntityQueryEnumerator<OldManComponent>();
        while (query.MoveNext(out var uid, out var comp))
            ProcessTeleport(uid, comp, now);
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

            trans.SetCoordinates(target, coords.Value);
            var victim = EnsureComp<DimensionVictimComponent>(target);
            victim.OldMan = (uid, comp);
            EnsureComp<NoirOverlayComponent>(target);
            audio.PlayPvs(victim.DimensionEnterSound, target);
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
        appearance.SetData(uid, DamageVisualizerKeys.DamageUpdateGroups, TeleportState.In);
        audio.PlayPvs(comp.TeleportSound, uid);
        comp.TPState = TeleportState.In;
        comp.TeleportationInEndAt = timing.CurTime + comp.TeleportInDuration;
        args.Handled = true;
    }

    private void OnPolyMorphRevert(Entity<OldManPolymorphComponent> uid, ref PolymorphedEvent args)
    {
        if (!args.IsRevert)
            return;

        if (!TryComp<OldManComponent>(args.NewEntity, out var comp))
            return;

        var oldmanTrans = Transform(args.NewEntity);
        Log.Info($"oldmangridUid: {oldmanTrans.GridUid}");
        Log.Info($"PreviousGridUid: {comp.PreviousGrid}");
        if ((oldmanTrans.GridUid == null || oldmanTrans.GridUid != comp.PreviousGrid) && comp.FallBackCoords != null)
            trans.SetCoordinates(args.NewEntity, comp.FallBackCoords.Value);

        comp.TPState = TeleportState.Out;
        comp.TeleportationOutEndAt = timing.CurTime + comp.TeleportOutDuration;
        appearance.SetData(args.NewEntity, DamageVisualizerKeys.DamageUpdateGroups, comp.TPState);
        audio.PlayPvs(comp.TeleportSound, uid);
        EnsureComp<AdminFrozenComponent>(args.NewEntity);
        comp.PolyMorphEntity = null;
    }
    private void OnPolyMorph(EntityUid uid, OldManComponent comp, ref PolymorphedEvent args)
    {
        comp.PolyMorphEntity = args.NewEntity;
    }

    private void OnSLeep(EntityUid uid, OldManComponent comp, ref SleepStateChangedEvent args)
    {
        _ambient.SetAmbience(uid, !args.FellAsleep);
    }
    protected virtual void RevertPolymorph(OldManComponent comp)
    {
    }
    private void OnVictimStateChanged(EntityUid uid, DimensionVictimComponent component, MobStateChangedEvent args)
    {
        if (args.OldMobState != MobState.Alive)
            return;

        var deadResult = _proto.Index<WeightedRandomPrototype>(component.DeadResults).Pick(_random);
        switch (deadResult)
        {
            case "Kill":
                _mobState.ChangeMobState(uid, MobState.Dead, origin: component.OldMan);
                ReturnVictimOnStation(uid, component);
                break;
            case "Revive":
                popup.PopupEntity("П О Д Н И М А Й С Я", uid, PopupType.SmallCaution);//туду в фтл
                if (TryComp<DamageableComponent>(uid, out var damagComp))
                    damageable.SetAllDamage((uid, damagComp), 0);
                _mobState.ChangeMobState(uid, MobState.Alive, origin: component.OldMan);
                break;
            case "Eat":
                if (TryComp<PerishableComponent>(uid, out var perish))
                    perish.RotAccumulator = perish.RotAfter;
                var sleep = EnsureComp<SleepingComponent>(component.OldMan.Owner);
                sleep.WakeThreshold = FixedPoint2.New(3);
                sleep.CooldownEnd = timing.CurTime + TimeSpan.FromMinutes(120);
                _mobState.ChangeMobState(uid, MobState.Dead, origin: component.OldMan);
                RevertPolymorph(component.OldMan.Comp);
                ReturnVictimOnStation(uid, component);
                break;
        }
    }
    private void OnCollide(EntityUid uid, DimensionEscapeTeleportComponent comp, ref StartCollideEvent args)
    {
        if (!TryComp<DimensionVictimComponent>(args.OtherEntity, out var victim))
            return;

        QueueDel(uid);

        if (comp.IsFake)
            return;

        ReturnVictimOnStation(args.OtherEntity, victim);
    }

    private void OnRefreshMoveSpeed(EntityUid uid, DimensionVictimComponent component, RefreshMovementSpeedModifiersEvent args)
    {
        args.ModifySpeed(0.5f, 0.5f);
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
            appearance.SetData(uid, DamageVisualizerKeys.DamageUpdateGroups, comp.TPState);
            RemComp<AdminFrozenComponent>(uid);
        }
    }
    /// <summary>
    /// Управление анимациями телепортации итд
    /// </summary>
    protected virtual void ProcessTeleport(EntityUid uid, OldManComponent comp, TimeSpan now)
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
        }
        //вышли из телепорта
        if (comp.TPState == TeleportState.Out && now >= comp.TeleportationOutEndAt)
        {
            comp.TPState = TeleportState.NoTP;
            appearance.SetData(uid, DamageVisualizerKeys.DamageUpdateGroups, comp.TPState);
            RemComp<AdminFrozenComponent>(uid);
        }
    }
    /// <summary>
    /// возвращает конкретного персонажа на конкретные координат
    /// </summary>
    private void ReturnVictimOnCoords(EntityUid uid, DimensionVictimComponent comp, EntityCoordinates targetCoords)
    {
        trans.SetCoordinates(uid, targetCoords);
        RemComp<DimensionVictimComponent>(uid);
        RemComp<NoirOverlayComponent>(uid);
        RemCompDeferred<JitteringComponent>(uid);
        audio.PlayPvs(comp.DimensionEscapeSound, uid);
        popup.PopupEntity($"{Name(uid)} падает с потолка", uid, PopupType.LargeCaution);//туду в фтл
        comp.Stream = audio.Stop(comp.Stream);
        foreach (var portal in comp.Portals)
        {
            if (Exists(portal) && !Deleted(portal))
                QueueDel(portal);
        }
    }
    /// <summary>
    /// возвращает конкретного персонажа на станцию
    /// </summary>
    private void ReturnVictimOnStation(EntityUid uid, DimensionVictimComponent comp)
    {
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

            ReturnVictimOnCoords(uid, comp, Transform(target).Coordinates);
            return;
        }

        //2. Если не получилось, то просто тпшимся на грид с которого уходили
        if (TryGetRandomExistingTile(grid, out var coords))
            ReturnVictimOnCoords(uid, comp, coords.Value);
    }
    /// <summary>
    /// Ищет свободный тайл у грида
    /// </summary>
    public bool TryGetRandomExistingTile(EntityUid gridUid, [NotNullWhen(true)] out EntityCoordinates? coords)
    {
        coords = null;
        if (!Exists(gridUid) || Deleted(gridUid))
            return false;

        if (!TryComp<MapGridComponent>(gridUid, out var grid))
            return false;

        var tiles = mapSystem.GetAllTiles(gridUid, grid).ToList();
        _random.Shuffle(tiles);//туду предиктед рандом
        foreach (var tile in tiles)
        {
            if (_turf.IsTileBlocked(tile, CollisionGroup.MobMask))
                continue;

            coords = new EntityCoordinates(gridUid, tile.GridIndices);
            return true;
        }

        return false;
    }
    /// <summary>
    /// Возврат всех жертв обратно на станцию
    /// </summary>
    public void ReturnAllVictims(Entity<OldManComponent> OldMan)
    {
        var victimQuery = EntityQueryEnumerator<DimensionVictimComponent>();
        while (victimQuery.MoveNext(out var uid, out var comp))
        {
            if (comp.OldMan == OldMan) ReturnVictimOnCoords(uid, comp, Transform(OldMan.Owner).Coordinates);
        }
    }
}
