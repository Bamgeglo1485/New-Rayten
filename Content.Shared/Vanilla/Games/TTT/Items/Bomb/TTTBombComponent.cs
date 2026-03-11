using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
namespace Content.Shared.Vanilla.Games.Items.TTT;

[RegisterComponent, NetworkedComponent]
public sealed partial class TTTBombComponent : Component
{
    /// <summary>
    /// тот, кто активировал бомбу
    /// </summary>
    [ViewVariables]
    public EntityUid? User;
    [ViewVariables]
    public float DifuseChance;
}
/// <summary>
/// компонент у предмета, позволяющий моментально обезвредить бомбу
/// </summary>
[RegisterComponent]
public sealed partial class DifusalKitComponent : Component
{
}





[Serializable, NetSerializable]
public sealed partial class TTTDefuseIvent : SimpleDoAfterEvent
{
}
