using Robust.Shared.Prototypes;

namespace Content.Shared.Vanilla.HealForDamage;

[RegisterComponent]
public sealed partial class HealForDamageComponent : Component
{

    [DataField("radius")]
    public float Radius = 4f; 

    [DataField("healMultiplier")]
    public float HealMultiplier = 2f;

}
