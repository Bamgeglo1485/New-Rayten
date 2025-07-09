using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
namespace Content.Shared.Vanilla.Dominator;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DangerMobComponent : Component
{
    [DataField, AutoNetworkedField]
    public int Danger = 10;
}
