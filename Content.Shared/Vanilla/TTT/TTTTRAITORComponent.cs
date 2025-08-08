using Robust.Shared.Audio;
using Content.Shared.StatusIcon;
using Robust.Shared.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared.Vanilla.TDM;

/// <summary>
/// This is used for tagging a mob as a nuke operative.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class TTTTRAITORComponent : Component
{
    [DataField("syndStatusIcon", customTypeSerializer: typeof(PrototypeIdSerializer<FactionIconPrototype>))]
    public string SyndStatusIcon = "SyndicateFaction";
}
[RegisterComponent, NetworkedComponent]
public sealed partial class ShowTTTTraitorsIconsComponent : Component {}

[RegisterComponent, NetworkedComponent]
public sealed partial class TTTDetectiveComponent : Component
{
    [DataField("DecStatusIcon", customTypeSerializer: typeof(PrototypeIdSerializer<FactionIconPrototype>))]
    public string DecStatusIcon = "TTTDetectiveFaction";
}
[RegisterComponent, NetworkedComponent]
public sealed partial class ShowTTTDetectiveIconsComponent : Component {}