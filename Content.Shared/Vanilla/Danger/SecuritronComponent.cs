using Content.Shared.Radio;
using Robust.Shared.Prototypes;
using Robust.Shared.Containers;
namespace Content.Shared.Vanilla.Dominator;

[RegisterComponent]
public sealed partial class SecuritronComponent : Component
{
    [ViewVariables]
    public ContainerSlot HandCuffContainer = default!;
    [DataField]
    public ProtoId<RadioChannelPrototype> SecurityChannel = "Security";
}
