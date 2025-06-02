using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Vanilla.Felinid;

[RegisterComponent, NetworkedComponent]
public sealed partial class MobFelinidComponent : Component
{
    [DataField("sprintBonus")]
    public float SprintSpeedBonus = 1.05f; // 5% бонус к скорости
}
