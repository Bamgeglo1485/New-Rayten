using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Damage.Components;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Vanilla.Entities.SpacetimeBoots;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Inventory.Events;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.FixedPoint;
using Robust.Shared.Map;
using Robust.Shared.GameObjects;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;
using System.Numerics;


namespace Content.Server.Vanilla.Entities.SpacetimeBoots;

public sealed class SpacetimeAbilitySystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _trans = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainerSystem = default!;
    [Dependency] private readonly SharedBloodstreamSystem _blood = default!;


    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SpacetimeAbilityComponent, MapInitEvent>(OnInit);
        SubscribeLocalEvent<SpacetimeAbilityComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<SpacetimeAbilityComponent, GotEquippedEvent>(OnEquip);
        SubscribeLocalEvent<SpacetimeAbilityComponent, SpacetimeJumpEvent>(OnSpacetimeJump);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTick.Value % 30 != 0)
            return;

        var query = EntityQueryEnumerator<SpacetimeAbilityComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            var target = comp.Wearer != null ? comp.Wearer.Value : uid;
            //кровь
            float? bloodAmount = null;
            float? bleedAmount = null;
            if (TryComp<BloodstreamComponent>(target, out var bloodstream) &&
                _solutionContainerSystem.ResolveSolution(target, bloodstream.BloodSolutionName, ref bloodstream.BloodSolution, out var bloodSolution))
            {
                bloodAmount = bloodSolution.FillFraction;
                bleedAmount = bloodstream.BleedAmount;
            }
            //урон
            DamageSpecifier damageCopy;
            if (TryComp<DamageableComponent>(target, out var damage))
                damageCopy = new DamageSpecifier(damage.Damage);
            else
                damageCopy = new DamageSpecifier();
            //пространство
            var worldPos = _trans.GetWorldPosition(uid);
            //сохраняем
            comp.History.Enqueue((worldPos, damageCopy, bloodAmount, bleedAmount, target));
            //удаляем лишнее
            while (comp.History.Count > comp.MaxSamples)
                comp.History.Dequeue();
        }
    }


    private void OnSpacetimeJump(Entity<SpacetimeAbilityComponent> entity, ref SpacetimeJumpEvent args)
    {
        if (entity.Comp.History.Count == 0)
            return;

        var uid = args.Performer;
        var (position, damage, bloodAmount, bleedAmount, SavedEnt) = entity.Comp.History.Dequeue();

        if (SavedEnt != uid)
            return;

        args.Handled = true;

        //визуальные эфекты
        var mapCoords = _trans.GetMapCoordinates(uid);
        Spawn("MobParadoxTimed", mapCoords);
        _audio.PlayPvs(entity.Comp.JumpSound, uid);
        // Перемещение
        mapCoords = new MapCoordinates(position, _trans.GetMapId(uid));
        var coords = _trans.ToCoordinates(mapCoords);
        _trans.SetCoordinates(uid, coords);
        _trans.AttachToGridOrMap(uid, Transform(uid));
        // Останавливаем движение тела после перемещения
        if (TryComp<PhysicsComponent>(uid, out var body))
        {
            _physics.SetLinearVelocity(uid, Vector2.Zero, body: body);
            _physics.SetAngularVelocity(uid, 0f, body: body);
        }
        //Здоровье
        _damageable.SetDamage((uid, CompOrNull<DamageableComponent>(uid)), damage);
        //кровь
        if (bloodAmount == null || bleedAmount == null || !TryComp<BloodstreamComponent>(uid, out var bloodstream))
            return;

        if (_solutionContainerSystem.ResolveSolution(uid, bloodstream.BloodSolutionName, ref bloodstream.BloodSolution, out var bloodSolution))
        {
            _blood.TryModifyBloodLevel((uid, bloodstream), -1f*(bloodSolution.FillFraction - bloodAmount.Value));
            _blood.TryModifyBleedAmount((uid, bloodstream), -1f*(bloodstream.BleedAmount - bleedAmount.Value));
        }

    }

    private void OnEquip(Entity<SpacetimeAbilityComponent> entity, ref GotEquippedEvent args)
    {
        entity.Comp.Wearer = args.Equipee;
    }

    private void OnInit(Entity<SpacetimeAbilityComponent> entity, ref MapInitEvent args)
    {
        if (!TryComp(entity, out ActionsComponent? comp))
            return;

        _actions.AddAction(entity, ref entity.Comp.ActionEntity, entity.Comp.Action, component: comp);
    }

    private void OnShutdown(Entity<SpacetimeAbilityComponent> entity, ref ComponentShutdown args)
    {
        _actions.RemoveAction(entity.Owner, entity.Comp.ActionEntity);
    }

}
