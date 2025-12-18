using Content.Shared.Vanilla.Archon.OldMan;
using Content.Shared.Damage.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Mobs.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Pinpointer;
using Content.Shared.Jittering;
using Content.Shared.Humanoid;
using Content.Shared.Overlays;
using Content.Shared.Damage;
using Content.Shared.Popups;
using Content.Shared.Audio;
using Content.Shared.Mobs;

using Content.Server.Station.Components;

using Robust.Server.GameObjects;

using Robust.Shared.Physics.Events;
using Robust.Shared.Audio.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Timing;
using Robust.Shared.Random;
using Robust.Shared.Maths;
using Robust.Shared.Map;

using System.Collections.Generic;
using System.Numerics;
using System.Linq;

namespace Content.Server.Vanilla.Archon.OldMan;

public sealed class PocketDimensionSystem : EntitySystem
{
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private readonly DamageableSystem _damageableSystem = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly MobStateSystem _mobStateSystem = default!;
    [Dependency] private readonly SharedJitteringSystem _jitter = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private TimeSpan _nextUpdate = TimeSpan.Zero;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DimensionVictimComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMoveSpeed);
        SubscribeLocalEvent<DimensionEscapeTeleportComponent, StartCollideEvent>(OnCollide);
        SubscribeLocalEvent<DimensionVictimComponent, MapInitEvent>(OnVictimInit);
        SubscribeLocalEvent<PocketDimensionComponent, MapInitEvent>(OnDimensionInit);
    }

    private void OnRefreshMoveSpeed(EntityUid uid, DimensionVictimComponent component, RefreshMovementSpeedModifiersEvent args)
    {
        args.ModifySpeed(0.5f, 0.5f);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;

        if (curTime < _nextUpdate)
            return;

        _nextUpdate = curTime + TimeSpan.FromSeconds(1);

        var victimQuery = EntityQueryEnumerator<DimensionVictimComponent>();
        var dimensionQuery = EntityQueryEnumerator<PocketDimensionComponent>();

        while (victimQuery.MoveNext(out var uid, out var comp))
        {
            VictimUpdate(uid, comp);
        }

        while (dimensionQuery.MoveNext(out var uid, out var comp))
        {
            if (comp.NextSpawn > curTime)
                continue;

            DimensionsUpdate(uid, comp);

            comp.NextSpawn = curTime + comp.TeleportsSpawnInterval;
        }
    }

    private void VictimUpdate(EntityUid uid, DimensionVictimComponent comp)
    {
        _jitter.AddJitter(uid, 2, 2);

        var curTime = _timing.CurTime;

        if (curTime >= comp.NextDamage)
        {
            comp.NextDamage = curTime + comp.DamageInterval;

            if (comp.DamageSound != null)
                _audio.PlayPvs(comp.DamageSound, uid);

            _popup.PopupEntity("Кожа гниёт на глазах", uid, PopupType.SmallCaution);

            if (_mobStateSystem.IsAlive(uid))
                _damageableSystem.TryChangeDamage(uid, comp.Damage);
        }

        if (!_mobStateSystem.IsAlive(uid))
        {
            if (_random.Prob(0.8f))
            {
                _damageableSystem.TryChangeDamage(uid, comp.FinalDamage);

                TeleportToPlayer(uid, comp, Transform(uid));
            }
            else 
            {

                var mapUid = Transform(uid).MapUid;

                if (mapUid == null)
                    return;

                _popup.PopupEntity("П О Д Н И М А Й С Я", uid, PopupType.SmallCaution);

                var coords = new EntityCoordinates(mapUid.Value, new Vector2(0, 0));

                _transform.SetCoordinates(uid, coords);

                if (TryComp<DamageableComponent>(uid, out var damagComp))
                    _damageableSystem.SetAllDamage((uid, damagComp), 0);
            }
        }
    }

    private void DimensionsUpdate(EntityUid uid, PocketDimensionComponent comp)
    {
        var mapId = Transform(uid).MapID;

        var teleportsToRemove = new List<EntityUid>();
        var teleportQuery = EntityQueryEnumerator<DimensionEscapeTeleportComponent, TransformComponent>();

        while (teleportQuery.MoveNext(out var teleportUid, out _, out var teleportXform))
        {
            if (teleportXform.MapID == mapId)
            {
                teleportsToRemove.Add(teleportUid);
            }
        }

        foreach (var teleport in teleportsToRemove)
        {
            QueueDel(teleport);
        }

        var spawnPoints = new List<EntityUid>();
        var spawnPointQuery = EntityQueryEnumerator<DimensionEscapeSpawnpointComponent, TransformComponent>();

        while (spawnPointQuery.MoveNext(out var spawnUid, out _, out var spawnXform))
        {
            if (spawnXform.MapID == mapId)
            {
                spawnPoints.Add(spawnUid);
            }
        }

        if (spawnPoints.Count == 0)
            return;

        var shuffled = spawnPoints.OrderBy(x => _random.Next()).ToList();

        for (var i = 0; i < Math.Min(comp.TeleportsAmount, shuffled.Count); i++)
        {
            var coordinates = Transform(shuffled[i]).Coordinates;
            Spawn(comp.TeleportPrototype, coordinates);
        }

        var startIndex = Math.Min(comp.TeleportsAmount, shuffled.Count);
        for (var i = startIndex; i < Math.Min(startIndex + comp.FakeTeleportsAmount, shuffled.Count); i++)
        {
            var coordinates = Transform(shuffled[i]).Coordinates;
            Spawn(comp.FakeTeleportPrototype, coordinates);
        }
    }

    private void OnVictimInit(EntityUid uid, DimensionVictimComponent comp, ref MapInitEvent args)
    {
        comp.NextDamage = _timing.CurTime + comp.DamageInterval;

        if (comp.DimensionAmbient != null)
            _audio.PlayGlobal(comp.DimensionAmbient, uid);
    }

    private void OnDimensionInit(EntityUid uid, PocketDimensionComponent comp, ref MapInitEvent args)
    {
        comp.NextSpawn = _timing.CurTime;
    }

    public void TeleportToPlayer(EntityUid uid, DimensionVictimComponent comp, TransformComponent transComp)
    {
        var validMinds = new List<EntityUid>();

        var mindQuery = EntityQueryEnumerator<HumanoidAppearanceComponent, TransformComponent>();

        while (mindQuery.MoveNext(out var targetUid, out _, out var xform))
        {
            if (_mobStateSystem.IsAlive(targetUid) &&
                !_container.IsEntityOrParentInContainer(targetUid) &&
                !HasComp<DimensionVictimComponent>(targetUid) &&
                IsOnStationGrid(targetUid))
                validMinds.Add(targetUid);
        }

        if (validMinds.Count == 0)
            return;

        var target = _random.Pick(validMinds);

        _transform.SetCoordinates(uid, transComp, Transform(target).Coordinates);

        _popup.PopupEntity("Чей-то обезображенный труп падает с потолка", uid, PopupType.LargeCaution);
        Spawn(comp.EscapeEffect, Transform(uid).Coordinates);

        RemComp<DimensionVictimComponent>(uid);
        RemComp<NoirOverlayComponent>(uid);
        RemCompDeferred<JitteringComponent>(uid);

        if (comp.DimensionEscapeSound != null)
            _audio.PlayPvs(comp.DimensionEscapeSound, uid);
    }

    public void TeleportToWarpPoint(EntityUid uid, DimensionVictimComponent comp, TransformComponent transComp)
    {
        var valids = new List<EntityUid>();

        var mindQuery = EntityQueryEnumerator<ConfigurableNavMapBeaconComponent, TransformComponent>();

        while (mindQuery.MoveNext(out var targetUid, out _, out var xform))
        {
            if (!_container.IsEntityOrParentInContainer(targetUid) &&
                IsOnStationGrid(targetUid))
                valids.Add(targetUid);
        }

        if (valids.Count == 0)
            return;

        var target = _random.Pick(valids);

        _transform.SetCoordinates(uid, transComp, Transform(target).Coordinates);

        Spawn(comp.EscapeEffect, Transform(uid).Coordinates);

        RemComp<DimensionVictimComponent>(uid);
        RemComp<NoirOverlayComponent>(uid);
        RemCompDeferred<JitteringComponent>(uid);

        if (comp.DimensionEscapeSound != null)
            _audio.PlayPvs(comp.DimensionEscapeSound, uid);
    }

    private bool IsOnStationGrid(EntityUid uid)
    {
        var transform = Transform(uid);

        if (transform.GridUid == null)
            return false;

        if (!HasComp<BecomesStationComponent>(transform.GridUid))
            return false;

        return true;
    }

    private void OnCollide(EntityUid uid, DimensionEscapeTeleportComponent comp, ref StartCollideEvent args)
    {
        var subject = args.OtherEntity;

        if (Transform(subject).Anchored)
            return;

        if (!TryComp<DimensionVictimComponent>(subject, out var victim))
            return;

        if (!comp.Fake && !comp.TeleportToPlayer)
            TeleportToWarpPoint(subject, victim, Transform(subject));
        else if (!comp.Fake && comp.TeleportToPlayer)
            TeleportToPlayer(uid, victim, Transform(subject));
        else
            QueueDel(uid);
    }
}
