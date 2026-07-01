using Content.Shared.Clothing.Components;
using Content.Shared.Gravity;
using Content.Shared.Inventory;
using Content.Shared.Standing;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Robust.Shared.Audio.Systems;
using Content.Shared.Popups;
using Content.Shared.Movement.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.IdentityManagement;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Damage.Components;
using Content.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.Effects;
using Robust.Shared.Player;

namespace Content.Shared.Vanilla.CompoundZ;

public sealed partial class SharedFlySystem : EntitySystem
{
    [Dependency] private StandingStateSystem _standing = default!;
    [Dependency] private SharedGravitySystem _gravity = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private MovementSpeedModifierSystem _movementSpeedModifier = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedColorFlashEffectSystem _color = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<FlyerComponent, FlyActionEvent>(OnFlyEvent);
        SubscribeLocalEvent<FlyerComponent, MapInitEvent>(OnInit);
        SubscribeLocalEvent<FlyerComponent, ComponentShutdown>(OnShutdown);

        SubscribeLocalEvent<FlyerComponent, IsWeightlessEvent>(OnIsWeightless);
        SubscribeLocalEvent<FlyerComponent, DownedEvent>(OnDowned);
        SubscribeLocalEvent<FlyerComponent, StoodEvent>(OnStood);

        SubscribeLocalEvent<FlyerComponent, RefreshWeightlessModifiersEvent>(OnRefreshWeightlessModifiers);

        SubscribeLocalEvent<FlyerComponent, StartCollideEvent>(OnStartCollide);
    }

    private void OnRefreshWeightlessModifiers(EntityUid uid, FlyerComponent comp, ref RefreshWeightlessModifiersEvent args)
    {
        if (comp.IsFlying)
            args.ModifyAcceleration(comp.FlySpeedModifier);
    }

    private void OnIsWeightless(Entity<FlyerComponent> entity, ref IsWeightlessEvent args)
    {
        if (args.Handled || !entity.Comp.IsFlying)
            return;

        if (_standing.IsDown(entity.Owner))
        {
            MakeUnfly(entity.Owner, entity.Comp);
            return;
        }

        args.Handled = true;
        args.IsWeightless = true;
    }

    private void OnDowned(Entity<FlyerComponent> entity, ref DownedEvent args)
    {
        if (entity.Comp.IsFlying)
            MakeUnfly(entity.Owner, entity.Comp);
    }

    private void OnStood(Entity<FlyerComponent> entity, ref StoodEvent args)
    {
        _gravity.RefreshWeightless(entity.Owner);
    }

    public void OnFlyEvent(EntityUid uid, FlyerComponent comp, ref FlyActionEvent args)
    {
        if (!comp.IsFlying && _standing.IsDown(uid))
            return;

        if (!comp.IsFlying)
            MakeFly(uid, comp);
        else
            MakeUnfly(uid, comp);

        args.Handled = true;
    }

    public void MakeFly(EntityUid uid, FlyerComponent comp)
    {
        comp.IsFlying = true;
        Dirty(uid, comp);

        EnsureComp<MovementAlwaysTouchingComponent>(uid, out var ass);
        _gravity.RefreshWeightless(uid);
        _movementSpeedModifier.RefreshWeightlessModifiers(uid);

        if (comp.FlyedSound != null)
            _audio.PlayPredicted(comp.FlyedSound, uid, uid);
        _popup.PopupEntity($"{Identity.Entity(uid, EntityManager)} взлетает ввысь!", uid);

    }

    public void MakeUnfly(EntityUid uid, FlyerComponent comp)
    {
        comp.IsFlying = false;
        Dirty(uid, comp);

        if (HasComp<MovementAlwaysTouchingComponent>(uid))
            RemComp<MovementAlwaysTouchingComponent>(uid);
        _gravity.RefreshWeightless(uid);
        _movementSpeedModifier.RefreshWeightlessModifiers(uid);

        if (comp.UnflyedSound != null)
            _audio.PlayPredicted(comp.UnflyedSound, uid, uid);
        _popup.PopupEntity($"{Identity.Entity(uid, EntityManager)} опускается на землю", uid);
    }

    private void OnInit(Entity<FlyerComponent> entity, ref MapInitEvent args)
    {
        if (!TryComp(entity, out ActionsComponent? comp))
            return;

        _actions.AddAction(entity, ref entity.Comp.ActionEntity, entity.Comp.Action, component: comp);
    }

    private void OnShutdown(Entity<FlyerComponent> entity, ref ComponentShutdown args)
    {
        _actions.RemoveAction(entity.Owner, entity.Comp.ActionEntity);
        MakeUnfly(entity.Owner, entity.Comp);
    }

    private void OnStartCollide(Entity<FlyerComponent> entity, ref StartCollideEvent args)
    {
        if (!entity.Comp.IsFlying)
            return;

        if (HasComp<MobMoverComponent>(args.OtherEntity))
            return;

        if (!TryComp<PhysicsComponent>(entity.Owner, out var physics))
            return;

        var velocity = physics.LinearVelocity.Length();

        if (velocity < entity.Comp.MinCollisionSpeed)
            return;

        var otherEntity = args.OtherEntity;

        if (otherEntity == entity.Owner)
            return;

        if (HasComp<DamageableComponent>(otherEntity))
        {
            var damage = new DamageSpecifier()
            {
                DamageDict = new Dictionary<ProtoId<DamageTypePrototype>, FixedPoint2>
                {
                    { "Structural", (int)(entity.Comp.StructuralDamage * (velocity / entity.Comp.MinCollisionSpeed)) }
                }
            };

            _damageable.TryChangeDamage(otherEntity, damage, origin: entity.Owner);
            var filter = Filter.Pvs(otherEntity, entityManager: EntityManager);
            _color.RaiseEffect(Color.Red, new List<EntityUid> { otherEntity }, filter);
        }

        var selfDamage = new DamageSpecifier()
        {
            DamageDict = new Dictionary<ProtoId<DamageTypePrototype>, FixedPoint2>
            {
                { "Blunt", (int)(entity.Comp.UserBruteDamage * (velocity / entity.Comp.MinCollisionSpeed)) }
            }
        };

        _damageable.TryChangeDamage(entity.Owner, selfDamage);
    }
}
