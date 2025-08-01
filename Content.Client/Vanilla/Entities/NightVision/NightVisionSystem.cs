using Content.Shared.Vanilla.Entities.NightVision;
using Robust.Shared.Player;
using Robust.Client.GameObjects;

namespace Content.Client.Vanilla.Entities.NightVision;

public sealed class NightVisionSystem : EntitySystem
{
    [Dependency] private readonly PointLightSystem _pointLightSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<NightVisionComponent, LocalPlayerAttachedEvent>(OnAttached);
        SubscribeLocalEvent<NightVisionComponent, LocalPlayerDetachedEvent>(OnDetached);
    }

    private void OnAttached(EntityUid uid, NightVisionComponent component, LocalPlayerAttachedEvent args)
    {
        if (TryComp<PointLightComponent>(uid, out var pointLight))
        {
            _pointLightSystem.SetEnabled(uid, true, pointLight);
        }
    }

    private void OnDetached(EntityUid uid, NightVisionComponent component, LocalPlayerDetachedEvent args)
    {
        if (TryComp<PointLightComponent>(uid, out var pointLight))
        {
            _pointLightSystem.SetEnabled(uid, false, pointLight);
        }
    }
}
