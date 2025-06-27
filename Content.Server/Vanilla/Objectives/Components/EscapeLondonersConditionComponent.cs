using Content.Server.Objectives.Systems;

namespace Content.Server.Objectives.Components;

[RegisterComponent, Access(typeof(EscapeLondonersConditionSystem))]
public sealed partial class EscapeLondonersConditionComponent : Component
{
    [DataField("requiredPlayers")]
    public int RequiredPlayers = 10;
}
