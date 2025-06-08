using Robust.Shared.Prototypes;

namespace Content.Server.Vanilla.Jammer;

[RegisterComponent]
public sealed partial class StationJammerComponent : Component
{
    [DataField]
    public TimeSpan? JammerEndTime;

}
