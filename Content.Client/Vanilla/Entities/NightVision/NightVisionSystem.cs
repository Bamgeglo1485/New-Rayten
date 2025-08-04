using Content.Shared.Vanilla.Entities.NightVision;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Client.Vanilla.NightVision;
using Content.Client.Audio;
using Content.Client.Overlays;

using Robust.Shared.Player;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Audio;

namespace Content.Shared.Vanilla.Entities.NightVision;

public sealed class NightVisionSystem : EquipmentHudSystem<NightVisionOverlayComponent>
{
    [Dependency] private readonly PointLightSystem _pointLightSystem = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IOverlayManager _overlayMan = default!;

    private NightVisionOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<NightVisionComponent, LocalPlayerAttachedEvent>(OnAttached);
        SubscribeLocalEvent<NightVisionComponent, LocalPlayerDetachedEvent>(OnDetached);

        SubscribeLocalEvent<NightVisionOverlayComponent, ComponentInit>(OnNightVisionInit);
        SubscribeLocalEvent<NightVisionOverlayComponent, ComponentShutdown>(OnNightVisionShutdown);

        // SubscribeLocalEvent<NightVisionOverlayComponent, InventoryRelayedEvent<ComponentInit>>(
        //     (e, c, ev) => OnNightVisionInit(e, c, ev.Args));
        // SubscribeLocalEvent<NightVisionOverlayComponent, InventoryRelayedEvent<ComponentShutdown>>(
        //     (e, c, ev) => OnNightVisionShutdown(e, c, ev.Args));
        _overlay = new();
    }
    protected override void UpdateInternal(RefreshEquipmentHudEvent<NightVisionOverlayComponent> component)
    {
        base.UpdateInternal(component);
        _overlayMan.AddOverlay(_overlay);
    }

    protected override void DeactivateInternal()
    {
        base.DeactivateInternal();
        _overlayMan.RemoveOverlay(_overlay);
    }

    private void OnAttached(EntityUid uid, NightVisionComponent component, LocalPlayerAttachedEvent args)
    {
        switchlight(uid, true);
    }

    private void OnDetached(EntityUid uid, NightVisionComponent component, LocalPlayerDetachedEvent args)
    {
        switchlight(uid, false);
    }

    private void OnNightVisionInit(EntityUid uid, NightVisionOverlayComponent component, ComponentInit args)
    {
        switchlight(uid, true);
    }
    private void OnNightVisionShutdown(EntityUid uid, NightVisionOverlayComponent component, ComponentShutdown args)
    {
        switchlight(uid, false);
    }

    private void switchlight(EntityUid uid, bool enable)
    {
        if (TryComp<PointLightComponent>(uid, out var pointLight))
            _pointLightSystem.SetEnabled(uid, enable, pointLight);
    }

}
