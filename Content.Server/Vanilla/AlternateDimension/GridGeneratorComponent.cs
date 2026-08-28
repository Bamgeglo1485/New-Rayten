using Content.Shared.AlternateDimension;
using Robust.Shared.Prototypes;

namespace Content.Server.AlternateDimension;

[RegisterComponent]
public sealed partial class GridGeneratorComponent : Component
{
    [DataField(required: true)]
    public List<ProtoId<AlternateDimensionPrototype>> Dimensions = new();
}
