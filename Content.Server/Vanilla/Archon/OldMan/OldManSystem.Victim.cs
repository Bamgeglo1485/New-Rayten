using Content.Server.Vanilla.Objectives.Systems;
using Content.Shared.Atmos.Rotting;
using Content.Shared.Bed.Sleep;
using Content.Shared.Vanilla.Archon.OldMan;
using Content.Shared.Jittering;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Damage.Systems;
using Content.Shared.Damage.Components;
using Content.Shared.Popups;
using Content.Shared.Random.Helpers;
using Content.Shared.Humanoid;
using Content.Shared.Overlays;
using Content.Shared.FixedPoint;
using Robust.Shared.Map;
using Robust.Shared.Random;

namespace Content.Server.Vanilla.Archon.OldMan;

public sealed partial class OldManSystem : SharedOldManSystem
{
    [Dependency] private SharedJitteringSystem _jitter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private MobStateSystem _mobstateSystem = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private OldManEatConditionSystem _eatConditionSystem = default!;

    public void EatVictim(EntityUid target, EntityUid oldMan, bool returnVictim = true)
    {
        if (!TryComp<OldManComponent>(oldMan, out var comp))
            return;

        TeleportAnimation(target, false);
        var victim = EnsureComp<DimensionVictimComponent>(target);
        victim.OldMan = oldMan;
        victim.StationGridUid = comp.StationGridUid;
        victim.DimensionGridUid = comp.DimensionGridUid;
        victim.ReturnableVictim = returnVictim;

        for (var i = 0; i < victim.TeleportsAmount; i++)
        {
            if (TryGetRandomExistingTile(comp.DimensionGridUid, out var coords))
                victim.Portals.Add(Spawn(victim.TeleportPrototype, coords.Value));
        }
        for (var i = 0; i < victim.FakeTeleportsAmount; i++)
        {
            if (TryGetRandomExistingTile(comp.DimensionGridUid, out var coords))
                victim.Portals.Add(Spawn(victim.FakeTeleportPrototype, coords.Value));
        }
    }
    private void OnVictimStateChanged(EntityUid uid, DimensionVictimComponent component, MobStateChangedEvent args)
    {
        if (args.OldMobState != MobState.Alive)
            return;

        var deadResult = Proto.Index(component.DeadResults).Pick(_random);
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
                sleep.CooldownEnd = Timing.CurTime + TimeSpan.FromMinutes(120);
                _mobstateSystem.ChangeMobState(uid, MobState.Dead, origin: component.OldMan);
                RevertPolymorph(component.OldMan);
                ReturnVictimOnStation(uid, component);
                _eatConditionSystem.SetCompleted(uid, true);
                if (TryComp<PerishableComponent>(uid, out var perish))
                    perish.RotAccumulator = perish.RotAfter;
                break;
        }
    }

    private void DamageVictim(EntityUid uid, DimensionVictimComponent comp, TimeSpan now)
    {
        if (now < comp.NextDamage)
            return;
        if (Transform(uid).GridUid != comp.DimensionGridUid)
        {
            RemComp<DimensionVictimComponent>(uid);
            return;
        }
        comp.NextDamage = now + comp.DamageInterval;
        Audio.PlayPvs(comp.DamageSound, uid);
        _damageable.TryChangeDamage(uid, comp.Damage);
    }
    private void OnVictimInit(EntityUid uid, DimensionVictimComponent comp, ref MapInitEvent args)
    {
        if (!_mobstateSystem.IsAlive(uid))
        {
            ReturnVictimOnStation(uid, comp);
            return;
        }
        MovementSpeed.RefreshMovementSpeedModifiers(uid);
        comp.NextDamage = Timing.CurTime + comp.DamageInterval;
        EnsureComp<NoirOverlayComponent>(uid);
        Audio.PlayPvs(comp.DimensionEnterSound, uid);
        comp.Stream = Audio.PlayGlobal(comp.DimensionAmbient, uid)?.Entity;
        _jitter.AddJitter(uid, 2, 2);
    }

    public override void ReturnAllVictims(EntityUid oldMan)
    {
        var victimQuery = EntityQueryEnumerator<DimensionVictimComponent>();
        while (victimQuery.MoveNext(out var uid, out var comp))
        {
            if (comp.OldMan == oldMan) ReturnVictimOnStation(uid, comp);
        }
    }

    public override void ReturnVictimOnStation(EntityUid uid, DimensionVictimComponent comp)
    {
        var grid = comp.StationGridUid;
        if (!Exists(grid) || Deleted(grid))
            return;

        //1. тпшимся к другому игроку
        EntityUid? chosen = null;
        var count = 0;

        var query = EntityQueryEnumerator<TransformComponent, HumanoidProfileComponent>();
        while (query.MoveNext(out var target, out var trans, out _))
        {
            if (trans.GridUid != grid)
                continue;
            if (!_mobstateSystem.IsAlive(target))
                continue;

            count++;
            if (_random.Prob(1f / count))
                chosen = target;
        }
        if (chosen != null)
        {
            ReturnVictimOnCoords(uid, comp, Transform(chosen.Value).Coordinates);
            return;
        }
        //2. Если не получилось, то просто тпшимся на грид с которого уходили
        if (TryGetRandomExistingTile(grid, out var coords))
            ReturnVictimOnCoords(uid, comp, coords.Value);
    }
    private void ReturnVictimOnCoords(EntityUid uid, DimensionVictimComponent comp, EntityCoordinates targetCoords)
    {
        if (!comp.ReturnableVictim)
            return;
        Trans.SetCoordinates(uid, targetCoords);
        RemComp<DimensionVictimComponent>(uid);
        Audio.PlayPvs(comp.DimensionEscapeSound, uid);
        RaiseNetworkEvent(new FallAnimationEvent(GetNetEntity(uid)));
    }
}
