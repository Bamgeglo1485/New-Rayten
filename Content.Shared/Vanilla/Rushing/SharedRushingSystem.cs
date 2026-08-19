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

        SubscribeLocalEvent<InputMoverComponent, KnockedDownEvent>(OnDown);
    }

    // харкод да и похуй
    private void OnDown(Entity<InputMoverComponent> ent, ref KnockedDownEvent ev)
    {
        if (!ent.Comp.HasDirectionalMovement)
            return;

        if (_gravity.IsWeightless(ent.Owner))
            return;

        if (!TryComp<StaminaComponent>(ent, out var stamina) || stamina.CritThreshold - stamina.StaminaDamage <= 25)
            return;

        if (!TryComp<KnockedDownComponent>(ent, out var knockedDown))
            return;

        if (knockedDown.AutoStand)
            return;

        var transform = Transform(ent);
        var direction = transform.Coordinates.Offset(transform.LocalRotation.ToWorldVec());

        _throwing.TryThrow(ent, direction, 6f);
        _stamina.TakeStaminaDamage(ent, 25, null, ent, ent, ignoreResist: true);
    }
}
