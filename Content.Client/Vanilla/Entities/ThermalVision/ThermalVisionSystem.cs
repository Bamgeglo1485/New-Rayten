using Content.Shared.Vanilla.Entities.ThermalVision;
using Content.Shared.Inventory.Events;
using Content.Client.Vanilla.Overlays.ThermalVision;
using Content.Client.Overlays;
using Robust.Client.Graphics;

namespace Content.Client.Vanilla.Entities.ThermalVision;

public sealed partial class ThermalVisionSystem : EquipmentHudSystem<ThermalVisionOverlayComponent>
{
    [Dependency] private IOverlayManager _overlayMan = default!;

    private ThermalVisionOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();
        _overlay = new(12f);
    }

    protected override void UpdateInternal(RefreshEquipmentHudEvent<ThermalVisionOverlayComponent> component)
    {
        base.UpdateInternal(component);
        _overlayMan.AddOverlay(_overlay);
    }

    protected override void DeactivateInternal()
    {
        base.DeactivateInternal();
        _overlayMan.RemoveOverlay(_overlay);
    }
}
