using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Teleportation.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class PortalMapComponent : Component
{
    [DataField("updateRate")]
    public float UpdateRate = 60f;

    [ViewVariables]
    public TimeSpan NextCheckTime;
}
