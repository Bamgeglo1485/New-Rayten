using Robust.Shared.GameStates;

namespace Content.Shared.Overlays;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class GrainOverlayComponent : Component
{
    [DataField]
    [AutoNetworkedField]
    public float Exponent = 10f;

    [DataField]
    [AutoNetworkedField]
    public float Strength = 0.7f;
}
