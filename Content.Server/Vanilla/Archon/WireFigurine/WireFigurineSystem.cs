using Content.Shared.Vanilla.Archon.WireFigurine;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Gibbing;
using Content.Server.NPC.HTN;
using Content.Server.NPC;
using Content.Server.NPC.Systems;
using Content.Shared.Interaction;
using Content.Shared.Doors.Components;
using Robust.Shared.Prototypes;
using Content.Shared.Tag;
using Robust.Shared.Map;
using System.Numerics;
using Content.Shared.Sprite;
using Content.Shared.Mobs;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Systems;
using Content.Shared.Damage.Components;
using Content.Server.Destructible;
using System.Linq;
using Content.Shared.Destructible.Thresholds.Triggers;
using Content.Shared.Damage;
using Robust.Shared.Audio.Systems;
using Content.Shared.Doors.Systems;
using Robust.Shared.Physics;
using Content.Shared.Physics;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Shared.Physics.Systems;
using Content.Shared.Movement.Systems;
using Robust.Shared.Physics.Components;

namespace Content.Server.Vanilla.Archon.WireFigurine;

public sealed partial class WireFigurineSystem : EntitySystem
{
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly HTNSystem _htn = default!;
    [Dependency] private readonly NPCSystem _npc = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly SharedScaleVisualsSystem _scaleVisuals = default!;
    [Dependency] private readonly MobThresholdSystem _thresh = default!;
    [Dependency] private readonly SharedAudioSystem _audioSystem = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    private static readonly ProtoId<TagPrototype> WallTag = "Wall";
    private static readonly ProtoId<TagPrototype> StuctureTag = "Structure";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WireFigurineComponent, UserActivateInWorldEvent>(OnInteract);
        SubscribeLocalEvent<WireFigurineComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<WireFigurineComponent, EatMetal018DoAfterEvent>(OnEatMetalDoAfter);
        SubscribeLocalEvent<WireFigurineMainComponent, FigurineOrderActionEvent>(OnOrderAction);
        SubscribeLocalEvent<WireFigurineComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMoveSpeed);
    }
    private void OnRefreshMoveSpeed(EntityUid uid, WireFigurineComponent component, RefreshMovementSpeedModifiersEvent args)
    {
        args.ModifySpeed(component.SpeedModifier, component.SpeedModifier);
    }
    private void OnInteract(EntityUid uid, WireFigurineComponent comp, ref UserActivateInWorldEvent args)
    {
        var target = args.Target;

        if (HasComp<EatenMetalComponent>(target))
        {
            return;
        }
        if (!HasComp<DoorComponent>(target) && !_tag.HasTag(target, WallTag) && !_tag.HasTag(target, StuctureTag))
        {
            //popup
            return;
        }
        _audioSystem.PlayPvs(comp.SoundStructureDevour, uid);
        EnsureComp<EatenMetalComponent>(target);
        comp.EatenMetal = target;
        var reduction = MathF.Min(comp.Stage * 0.1f, 0.9f);
        var doAfterTime = comp.EatDoAfterTime * (1f - reduction);

        var doAfterEventArgs = new DoAfterArgs(EntityManager, uid, doAfterTime, new EatMetal018DoAfterEvent(), eventTarget: uid, target: target)
        {
            DistanceThreshold = 2f,
            BreakOnMove = true,
            BreakOnDamage = true
        };

        if (!_doAfter.TryStartDoAfter(doAfterEventArgs))
            return;
    }
    private void OnOrderAction(EntityUid uid, WireFigurineMainComponent component, FigurineOrderActionEvent args)
    {
        if (component.CurrentOrder == args.Order)
            return;

        args.Handled = true;

        component.CurrentOrder = args.Order;
        foreach (var figurine in component.FigurineCopies)
        {
            if (!TryComp<HTNComponent>(figurine, out var htn))
                continue;

            if (htn.Plan != null)
                _htn.ShutdownPlan(htn);

            _npc.SetBlackboard(figurine, NPCBlackboard.CurrentOrders, args.Order);
            _htn.Replan(htn);
        }
    }
    private void OnShutdown(Entity<WireFigurineComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.EatenMetal != null)
            RemComp<EatenMetalComponent>(ent.Comp.EatenMetal.Value);
        if (!TryGetMainFigurine(ent, out var mainFigurine))
            return;

        mainFigurine.Comp.FigurineCopies.Remove(ent.Owner);
        if (mainFigurine.Owner == ent.Owner)
        {
            foreach (var figurine in mainFigurine.Comp.FigurineCopies)
                QueueDel(figurine);
        }
    }

    private void OnEatMetalDoAfter(Entity<WireFigurineComponent> ent, ref EatMetal018DoAfterEvent args)
    {
        if (args.Target == null)
            return;
        RemComp<EatenMetalComponent>(args.Target.Value);
        ent.Comp.EatenMetal = null;
        if (args.Cancelled)
            return;

        var dealt = ApplyCappedDamage(args.Target.Value, ent.Comp.EatDamage, ent);

        ent.Comp.DamageSum += dealt.Float();

        if (ent.Comp.DamageSum < ent.Comp.DamageToReproduce)
            return;

        ent.Comp.DamageSum = 0;

        if (!TryGetMainFigurine(ent, out var mainFigurine))
            return;

        var newEnt = Spawn(ent.Comp.SpawnProto, Transform(ent).Coordinates);
        if (!TryComp<WireFigurineComponent>(newEnt, out var figurine))
        {
            Del(newEnt);
            return;
        }

        _npc.SetBlackboard(newEnt, NPCBlackboard.FollowTarget, new EntityCoordinates(mainFigurine, Vector2.Zero));
        _npc.SetBlackboard(newEnt, NPCBlackboard.CurrentOrders, mainFigurine.Comp.CurrentOrder);
        figurine.Main = mainFigurine.Owner;
        figurine.Stage = ent.Comp.Stage;
        mainFigurine.Comp.FigurineCopies.Add(newEnt);
        SetStage((newEnt, figurine), figurine.Stage);

        if (mainFigurine.Comp.FigurineCopies.Count >= mainFigurine.Comp.FigurineToNewStage)
        {
            if (TryComp<WireFigurineComponent>(mainFigurine, out var mainfigurinefigurineComp))
                SetStage(mainFigurine, mainfigurinefigurineComp.Stage + 1);
        }
    }
    private FixedPoint2 ApplyCappedDamage(
       EntityUid target,
       DamageSpecifier damage,
       EntityUid origin)
    {
        if (!TryComp<DamageableComponent>(target, out var damageable) ||
            !TryComp<DestructibleComponent>(target, out var destructible))
            return FixedPoint2.Zero;

        var currentDamage = damageable.TotalDamage;

        var nextThreshold = destructible.Thresholds
            .Select(t => t.Trigger)
            .OfType<DamageTrigger>()
            .Where(t => t.Damage > currentDamage)
            .OrderBy(t => t.Damage)
            .FirstOrDefault();

        if (nextThreshold == null)
            return FixedPoint2.Zero;

        var remaining = nextThreshold.Damage - currentDamage;
        if (remaining <= FixedPoint2.Zero)
            return FixedPoint2.Zero;

        var wantedTotal = damage.GetTotal();
        if (wantedTotal <= FixedPoint2.Zero)
            return FixedPoint2.Zero;

        var finalDamage = FixedPoint2.Min(wantedTotal, remaining);

        // Создаём новый урон ТОЛЬКО структурного типа
        var adjustedDamage = new DamageSpecifier
        {
            DamageDict = new()
            {
                { "Structural", finalDamage }
            }
        };
        _damageable.TryChangeDamage(
            target,
            adjustedDamage,
            origin: origin,
            ignoreResistances: true);

        return finalDamage;
    }
    private void SetStage(Entity<WireFigurineMainComponent> mainFigurine, int stage)
    {
        if (TryComp<WireFigurineComponent>(mainFigurine, out var mainfigurinefigurineComp))
            SetStage((mainFigurine, mainfigurinefigurineComp), stage);

        foreach (var figurine in mainFigurine.Comp.FigurineCopies)
            QueueDel(figurine);
    }
    private void SetStage(Entity<WireFigurineComponent> figurine, int stage)
    {
        figurine.Comp.Stage = stage;

        var stageScaleModifier = Math.Clamp(stage * 0.6f, 1f, 20f);
        var scale = new Vector2(stageScaleModifier, stageScaleModifier);
        _scaleVisuals.SetSpriteScale(figurine, scale);

        figurine.Comp.DamageSum = 0;

        var multiplier = MathF.Pow(2f, stage - 1);

        figurine.Comp.DamageToReproduce = figurine.Comp.BaseDamageToReproduce * multiplier;
        figurine.Comp.EatDamage = figurine.Comp.BaseEatDamage * multiplier;
        _thresh.SetMobStateThreshold(
            figurine,
            FixedPoint2.New(10 * multiplier),
            MobState.Dead);
        if (stage > 3)
        {
            _tag.AddTag(figurine, SharedDoorSystem.DoorBumpTag);
            if (!TryComp<FixturesComponent>(figurine, out var fixtures) || !TryComp<PhysicsComponent>(figurine, out var physics))
                return;

            var fixture = fixtures.Fixtures.First();
            _physics.SetCollisionMask(figurine, fixture.Key, fixture.Value, (int)CollisionGroup.MobMask, fixtures, physics);
            _physics.SetCollisionLayer(figurine, fixture.Key, fixture.Value, (int)CollisionGroup.MobLayer, fixtures, physics);
        }

        // минус 5% скорости за каждую стадию
        var slow = 1f * stage * 0.05f;

        // не даём упасть ниже 10%
        figurine.Comp.SpeedModifier = MathF.Max(0.1f, slow);
    }

    private bool TryGetMainFigurine(Entity<WireFigurineComponent> ent, out Entity<WireFigurineMainComponent> mainFigurine)
    {
        if (TryComp<WireFigurineMainComponent>(ent, out var comp))
        {
            mainFigurine = (ent.Owner, comp);
            return true;
        }

        if (!Exists(ent.Comp.Main) || TerminatingOrDeleted(ent.Comp.Main))
        {
            mainFigurine = default;
            return false;
        }

        if (TryComp(ent.Comp.Main, out comp))
        {
            mainFigurine = (ent.Comp.Main, comp);
            return true;
        }

        mainFigurine = default;
        return false;
    }
}
