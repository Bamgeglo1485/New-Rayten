using Content.Shared.Inventory.Events;
using Content.Shared.Overlays;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;
using Content.Shared.Vanilla.CCVars;

namespace Content.Client.Overlays;

public sealed partial class GrainOverlaySystem : EquipmentHudSystem<GrainOverlayComponent>
{
    [Dependency] private IOverlayManager _overlayMan = default!;
    [Dependency] private IConfigurationManager _cfg = default!;

    private GrainOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        _overlay = new GrainOverlay();
        _cfg.OnValueChanged(CCVVars.GrainStrength, OnGrainStrengthChanged, true);
    }

    private void OnGrainStrengthChanged(float value)
    {
        if (_overlay != null)
        {
            _overlay.Strength = value;
        }
    }

    protected override void UpdateInternal(RefreshEquipmentHudEvent<GrainOverlayComponent> component)
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
