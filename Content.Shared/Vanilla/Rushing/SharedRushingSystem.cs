using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.StatusEffectNew;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Gravity;

namespace Content.Shared.Vanilla.Rushing;

public sealed partial class SharedRushingSystem : EntitySystem
{
    [Dependency] private SharedGravitySystem _gravity = default!;
    [Dependency] private SharedStaminaSystem _stamina = default!;
    [Dependency] private ThrowingSystem _throwing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RusherComponent, KnockedDownEvent>(OnDown);
    }

    // харкод да и похуй
    private void OnDown(Entity<RusherComponent> ent, ref KnockedDownEvent ev)
    {
        if (!TryComp<InputMoverComponent>(ent, out var input))
            return;

        if (!input.HasDirectionalMovement)
            return;

        if (_gravity.IsWeightless(ent.Owner))
            return;

        if (!TryComp<StaminaComponent>(ent, out var stamina) || stamina.CritThreshold - stamina.StaminaDamage <= ent.Comp.StaminaLoss)
            return;

        if (!TryComp<KnockedDownComponent>(ent, out var knockedDown))
            return;

        if (knockedDown.AutoStand)
            return;

        var transform = Transform(ent);
        var direction = input.WishDir * ent.Comp.DistanceModifier;

        _throwing.TryThrow(ent, direction, ent.Comp.Speed);
        _stamina.TakeStaminaDamage(ent, ent.Comp.StaminaLoss, null, ent, ent, ignoreResist: true);
    }
}
