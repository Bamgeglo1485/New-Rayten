using Content.Shared.AlternateDimension;
using Robust.Shared.Prototypes;

namespace Content.Server.Backrooms;

[RegisterComponent]
public sealed partial class ZeroPointComponent : Component
{
    [DataField(required: true)]
    public ProtoId<AlternateDimensionPrototype> TargetDimension;
}
