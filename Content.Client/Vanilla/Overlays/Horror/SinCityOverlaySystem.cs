using Content.Shared.Inventory.Events;
using Content.Shared.Overlays;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;
using Content.Shared.Vanilla.CCVars;

namespace Content.Client.Overlays;

public sealed partial class SinCityOverlaySystem : EquipmentHudSystem<SinCityOverlayComponent>
{
    [Dependency] private IOverlayManager _overlayMan = default!;
    [Dependency] private IConfigurationManager _cfg = default!;

    private SinCityOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        _overlay = new SinCityOverlay();
        _cfg.OnValueChanged(CCVVars.SinCitySaturation, OnSinCitySaturationChanged, true);
    }

    private void OnSinCitySaturationChanged(float value)
    {
        if (_overlay != null)
        {
            _overlay.Saturation = value;
        }
    }

    protected override void UpdateInternal(RefreshEquipmentHudEvent<SinCityOverlayComponent> component)
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
