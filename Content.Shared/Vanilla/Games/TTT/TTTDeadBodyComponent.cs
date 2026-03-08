using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Vanilla.Games.TTT;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TTTDeadBodyComponent : Component
{
    [ViewVariables]
    public EntityUid RuleLink;

    [AutoNetworkedField, ViewVariables]
    public TimeSpan DeathTime;

    [AutoNetworkedField, ViewVariables]
    public EntityUid? Gun = null;

    [AutoNetworkedField, ViewVariables]
    public bool Confirmed = false;

    [ViewVariables]
    public EntityUid? Killer = null;
}

[NetSerializable, Serializable]
public enum BodyExamineUiKey : byte
{
    Key,
}
