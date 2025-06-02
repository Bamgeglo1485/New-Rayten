using Content.Shared.Movement.Systems;
using Robust.Shared.GameStates;

namespace Content.Shared.Vanilla.Felinid;

public sealed class FelinidSpeedSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MobFelinidComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
    }

    private void OnRefreshSpeed(EntityUid uid, MobFelinidComponent component, RefreshMovementSpeedModifiersEvent args)
    {
        args.ModifySpeed(1.0f, component.SprintSpeedBonus);
    }
}
