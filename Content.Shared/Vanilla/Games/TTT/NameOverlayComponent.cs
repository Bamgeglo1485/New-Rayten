using Content.Shared.Actions;
using Robust.Shared.GameStates;

namespace Content.Shared.Vanilla.Games.TTT;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NameOverlayComponent : Component
{
    [DataField, AutoNetworkedField]
    public string Name = "Турель";
    [DataField]
    public string OldName = "НЕИЗВЕСТНЫЙ";

    [DataField, AutoNetworkedField]
    public Color NameColor = Color.Green;
}
public sealed partial class TTTDisguiserActionEvent : InstantActionEvent
{
}
