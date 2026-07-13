using Robust.Shared.GameStates;

namespace Content.Shared.Overlays;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class SinCityOverlayComponent : Component
{
    [DataField]
    [AutoNetworkedField]
    public float Saturation = 0.6f;
}
