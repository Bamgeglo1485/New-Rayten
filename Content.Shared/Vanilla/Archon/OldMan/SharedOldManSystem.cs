using Content.Shared.Audio;
using Content.Shared.Administration;
using Content.Shared.Atmos.Rotting;
using Content.Shared.Bed.Sleep;
using Content.Shared.Movement.Systems;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Popups;
using Content.Shared.Polymorph;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Maps;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Physics;
using Content.Shared.Overlays;
using Content.Shared.Jittering;
using Content.Shared.Humanoid;
using Content.Shared.FixedPoint;
using Content.Shared.Random;
using Content.Shared.Random.Helpers;
using Robust.Shared.Map.Components;
using Robust.Shared.Map;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics.Events;
using Robust.Shared.Timing;
using Robust.Shared.Random;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection.Metadata;

namespace Content.Shared.Vanilla.Archon.OldMan;

public abstract class SharedOldManSystem : EntitySystem
{
    [Dependency] protected readonly SharedAppearanceSystem appearance = default!;
    [Dependency] protected readonly SharedAudioSystem audio = default!;
    [Dependency] protected readonly IGameTiming timing = default!;
    [Dependency] protected readonly SharedTransformSystem trans = default!;
    [Dependency] protected readonly SharedMapSystem mapSystem = default!;
    [Dependency] private readonly SharedAmbientSoundSystem _ambient = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private readonly TurfSystem _turf = default!;

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
        SubscribeLocalEvent<OldManComponent, OldManTeleportEvent>(OnTeleportEvent);
        SubscribeLocalEvent<OldManComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<OldManComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<DimensionVictimComponent, ComponentShutdown>(OnVictimShutDown);

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
    private void OnVictimShutDown(EntityUid uid, DimensionVictimComponent comp, ComponentShutdown args)
    {
        if (timing.IsFirstTimePredicted)
            Fall(uid);
        RemComp<NoirOverlayComponent>(uid);
        RemCompDeferred<JitteringComponent>(uid);
        comp.Stream = audio.Stop(comp.Stream);
        foreach (var portal in comp.Portals)
        {
            if (Exists(portal) && !Deleted(portal))
                PredictedQueueDel(portal);
        }
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
            victim.OldMan = uid;
            victim.StationGridUid = comp.StationGridUid;
            _movementSpeed.RefreshMovementSpeedModifiers(target);
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
        ReturnAllVictims(uid);
    }

    private void OnCollide(EntityUid uid, DimensionEscapeTeleportComponent comp, ref StartCollideEvent args)
    {
        if (!TryComp<DimensionVictimComponent>(args.OtherEntity, out var victim))
            return;

        PredictedQueueDel(uid);

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
        ReturnAllVictims(uid);

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
    public virtual void ReturnAllVictims(EntityUid oldMan)
    {
    }
    protected virtual void RevertPolymorph(EntityUid uid)
    {

    }
    protected virtual void Fall(EntityUid uid)
    {

    }
    public virtual void ReturnVictimOnStation(EntityUid uid, DimensionVictimComponent comp)
    {

    }
}
