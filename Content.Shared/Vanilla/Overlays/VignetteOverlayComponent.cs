using Robust.Shared.GameStates;

namespace Content.Shared.Overlays;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class VignetteOverlayComponent : Component
{
    [DataField]
    [AutoNetworkedField]
    public float OuterRadius = 13f;

    [DataField]
    [AutoNetworkedField]
    public float MainAlpha = 10f;
}
