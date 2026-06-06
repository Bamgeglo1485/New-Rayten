using Content.Server.Power.EntitySystems;
using Content.Shared.Vanilla.Archon.OldMan;
using Content.Shared.Vanilla.Archon.OldMan.FemurBreaker;
using Content.Shared.Verbs;
using Content.Shared.Hands.Components;
using Content.Shared.Buckle.Components;
using Content.Shared.Speech.Components;
using Content.Shared.Speech.Muting;
using Content.Shared.Humanoid;
using Content.Shared.CombatMode.Pacification;
using Content.Shared.ActionBlocker;
using Content.Shared.Actions;
using Content.Server.Polymorph.Systems;
using Content.Shared.Damage.Systems;
using Content.Shared.Jittering;
using Content.Shared.Mobs.Systems;
using Content.Shared.Bed.Sleep;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Utility;
using Robust.Shared.Timing;
using Robust.Shared.Random;
using Content.Shared.Mobs;
using Content.Server.Vanilla.Objectives.Systems;



namespace Content.Server.Vanilla.Archon.OldMan.FemurBreaker;

public sealed partial class FemurBreakerSystem : EntitySystem
{
    [Dependency] private PolymorphSystem _polymorph = default!;
    [Dependency] private MobStateSystem _mob = default!;
    [Dependency] private PowerReceiverSystem _power = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedJitteringSystem _jittering = default!;
    [Dependency] private OldManSystem _oldman = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private ActionBlockerSystem _blocker = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private OldManEatConditionSystem _eatConditionSystem = default!;

    private const float UpdateRate = 0.25f;
    private float _updateDif;
    public override void Initialize()
    {
        SubscribeLocalEvent<FemurBreakerComponent, GetVerbsEvent<AlternativeVerb>>(AddFemurBreakerVerb);
        SubscribeLocalEvent<OldManFoodComponent, UnbuckleAttemptEvent>(OnUnbuckleAttempt);
        SubscribeLocalEvent<OldManFoodComponent, MobStateChangedEvent>(OnMobStateChange);
    }

    private void OnMobStateChange(Entity<OldManFoodComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Alive)
            return;
        ent.Comp.AudioStream = _audio.Stop(ent.Comp.AudioStream);
        foreach (var oldManUid in ent.Comp.BaitedOldMans)
        {
            if (!Exists(oldManUid) || Deleted(oldManUid))
                continue;
            if (!TryComp<OldManComponent>(oldManUid, out var comp))
                continue;
            comp.Eats = false;
            _blocker.UpdateCanMove(oldManUid);
            _actions.SetEnabled(comp.ActionEnt, true);
            RemComp<PacifiedComponent>(oldManUid);
        }
        RemComp<OldManFoodComponent>(ent);
        RemComp<PacifiedComponent>(ent);
        RemComp<JitteringComponent>(ent);
    }

    private void AddFemurBreakerVerb(EntityUid uid, FemurBreakerComponent comp, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        if (_timing.CurTime <= comp.NextActivateAt)
            return;

        if (!HasComp<HandsComponent>(args.User))
            return;

        if (!_power.IsPowered(uid))
            return;

        AlternativeVerb verb = new()
        {
            Act = () =>
            {
                SwitchStateMechanism(uid, comp, FemurBreakerState.Down);
            },
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/cutlery.svg.192dpi.png")),
            Text = Loc.GetString("запустить механизм"),
            Priority = -3
        };
        args.Verbs.Add(verb);
    }
    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        _updateDif += frameTime;
        if (_updateDif < UpdateRate)
            return;
        _updateDif -= UpdateRate;

        var query = EntityQueryEnumerator<FemurBreakerComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (_timing.CurTime >= comp.FemurTimeAt)
                TryFemurBreak(uid, comp);

            if (_timing.CurTime >= comp.SwitchStateAt)
            {
                if (comp.CurrentState == FemurBreakerState.Up)
                    SwitchStateMechanism(uid, comp, FemurBreakerState.Static);

                if (comp.CurrentState == FemurBreakerState.Down)
                    SwitchStateMechanism(uid, comp, FemurBreakerState.Up);
            }
        }
        var foodQuery = EntityQueryEnumerator<OldManFoodComponent>();
        while (foodQuery.MoveNext(out var uid, out var comp))
        {
            if (_timing.CurTime >= comp.OldManWillBiteAt && !comp.OldMansBited)
                BiteOldMans((uid, comp));

            if (_timing.CurTime >= comp.WillKilledAt)
                Eat((uid, comp));
        }
    }
    //деды приходят и стоят
    private void BiteOldMans(Entity<OldManFoodComponent> food)
    {
        food.Comp.AudioStream = _audio.Stop(food.Comp.AudioStream);
        food.Comp.OldMansBited = true;
        EnsureComp<PacifiedComponent>(food.Owner);

        var query = EntityQueryEnumerator<OldManPolymorphComponent>();
        while (query.MoveNext(out var polyUid, out var polyComp))
        {
            if (Transform(food.Owner).GridUid != polyComp.StationGridUid)
                continue;

            _polymorph.Revert(polyUid);
        }

        var anyOldMans = false;
        var oldManQuery = EntityQueryEnumerator<OldManComponent>();
        while (oldManQuery.MoveNext(out var oldManUid, out var oldManComp))
        {
            if (Transform(food.Owner).GridUid != oldManComp.StationGridUid)
                continue;

            if (HasComp<SleepingComponent>(oldManUid))
                continue;
            if (!_mob.IsAlive(oldManUid))
                continue;
            if (oldManComp.Eats)
                continue;

            _transform.SetCoordinates(oldManUid, Transform(food.Owner).Coordinates.Offset(_random.NextVector2(1.5f)));
            _oldman.TeleportAnimation(oldManUid, true);

            var pacified = EnsureComp<PacifiedComponent>(oldManUid);
            pacified.DisallowAllCombat = true;
            oldManComp.Eats = true;
            _blocker.UpdateCanMove(oldManUid);

            _actions.SetEnabled(oldManComp.ActionEnt, false);

            Dirty(oldManUid, oldManComp);
            anyOldMans = true;
            food.Comp.BaitedOldMans.Add(oldManUid);
        }
        if (anyOldMans)
        {
            food.Comp.AudioStream = _audio.PlayPvs(food.Comp.EatSound, food.Owner)?.Entity;
            return;
        }
        RemComp<OldManFoodComponent>(food.Owner);
        RemComp<JitteringComponent>(food.Owner);

    }
    //дедусы хавают челика
    private void Eat(Entity<OldManFoodComponent> food)
    {
        foreach (var oldManUid in food.Comp.BaitedOldMans)
        {
            if (!Exists(oldManUid) || Deleted(oldManUid))
                continue;
            if (HasComp<SleepingComponent>(oldManUid))
                continue;
            if (!_mob.IsAlive(oldManUid))
                continue;
            if (!TryComp<OldManComponent>(oldManUid, out var comp))
                continue;
            if (!comp.Eats)
                continue;
            comp.Eats = false;
            _oldman.EatVictim(food.Owner, oldManUid, false);
            _damageable.TryChangeDamage(food.Owner, food.Comp.EatenDamage);
            var sleep = EnsureComp<SleepingComponent>(oldManUid);
            sleep.WakeThreshold = FixedPoint2.New(2);
            sleep.CooldownEnd = _timing.CurTime + TimeSpan.FromMinutes(120);
            RemComp<PacifiedComponent>(oldManUid);
            _blocker.UpdateCanMove(oldManUid);
            _actions.SetEnabled(comp.ActionEnt, true);
            _eatConditionSystem.SetCompleted(oldManUid, true);
        }
        RemComp<PacifiedComponent>(food.Owner);
        RemComp<OldManFoodComponent>(food.Owner);
        RemComp<JitteringComponent>(food.Owner);
    }
    private void TryFemurBreak(EntityUid uid, FemurBreakerComponent comp)
    {
        comp.FemurTimeAt = null;
        if (!TryComp<StrapComponent>(uid, out var strap) || strap.BuckledEntities.Count == 0)
            return;
        var anyFemurs = false;
        //проводим фемурчик
        foreach (var victim in strap.BuckledEntities)
        {
            _damageable.TryChangeDamage(victim, comp.FemurDamage);

            if (!HasComp<HumanoidProfileComponent>(victim))
                continue;
            if (!_mob.IsAlive(victim))
                continue;
            if (!HasComp<VocalComponent>(victim))
                continue;
            if (HasComp<MutedComponent>(victim))
                continue;
            _jittering.AddJitter(victim, 5, 20);
            anyFemurs = true;

            var food = EnsureComp<OldManFoodComponent>(victim);
            food.AudioStream = _audio.PlayPvs(food.FemurBreakSound, victim)?.Entity;
            food.OldManWillBiteAt = _timing.CurTime + comp.FemurBreakTime;//1. Приходит дед
            food.WillKilledAt = food.OldManWillBiteAt + comp.KillTime;//2. убийство
        }
        if (anyFemurs)
            TrySetNextActivateTime(comp, comp.FemurBreakTime + comp.KillTime);
    }
    private void SwitchStateMechanism(EntityUid uid, FemurBreakerComponent comp, FemurBreakerState newState)
    {
        _appearance.SetData(uid, FemurBreakerDeviceVisuals.State, newState);
        comp.CurrentState = newState;
        if (newState == FemurBreakerState.Down || newState == FemurBreakerState.Up)
        {
            _audio.PlayPvs(comp.ActivateSound, uid);
            comp.SwitchStateAt = _timing.CurTime + comp.ActivateTime;
            TrySetNextActivateTime(comp, comp.ActivateTime);
        }

        if (newState == FemurBreakerState.Down)
            comp.FemurTimeAt = _timing.CurTime + comp.ActivateToFemurTime;
    }
    //устанавливает момент времени, после которого разрешается заново активировать КПБ
    private void TrySetNextActivateTime(FemurBreakerComponent comp, TimeSpan cooldown)
    {
        var newTime = _timing.CurTime + cooldown;
        if (newTime > comp.NextActivateAt)
            comp.NextActivateAt = newTime;
    }
    private void OnUnbuckleAttempt(Entity<OldManFoodComponent> ent, ref UnbuckleAttemptEvent args)
    {
        args.Cancelled = true;
    }
}
