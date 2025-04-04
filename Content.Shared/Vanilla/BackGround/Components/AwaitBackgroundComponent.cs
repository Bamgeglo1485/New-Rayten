using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Vanilla.Background;

[RegisterComponent, NetworkedComponent]
public sealed partial class AwaitBackgroundComponent : Component
{

    [DataField("backgroundGroup"), ViewVariables(VVAccess.ReadOnly)]
    public ProtoId<BackgroundGroupPrototype>? BackgroundGroup;
}