using Content.Shared.FixedPoint;
using Content.Shared.Roles;
using Content.Shared.Storage;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Server.Vanilla.TDM;

[RegisterComponent]
public sealed partial class TDMRuleComponent : Component
{
    [DataField]
    public TimeSpan NextUpdate;
    [DataField]
    public TimeSpan TimeOnNewCycle = TimeSpan.FromSeconds(0);
    [DataField]
    public TimeSpan TimeToNewCycle = TimeSpan.FromSeconds(320);
    [DataField]
    public bool CountdownPlayed = false;
    [DataField]
    public bool OnlyOneCycle = false;
}
