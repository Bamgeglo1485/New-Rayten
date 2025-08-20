using Content.Shared.Vanilla.Entities.ThermalVision;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Client.Vanilla.Overlays;
using Content.Client.Overlays;

using Robust.Shared.Player;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;

namespace Content.Client.Vanilla.Entities.ThermalVision;

public sealed class ThermalVisionSystem : EquipmentHudSystem<ThermalVisionOverlayComponent>
{
    [Dependency] private readonly IOverlayManager _overlayMan = default!;

    private ThermalVisionOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();
        _overlay = new(7f,1f);
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
