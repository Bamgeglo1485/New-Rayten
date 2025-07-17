using Robust.Shared.Prototypes;

namespace Content.Shared.Atmos.Components;

[RegisterComponent]
public sealed partial class PressureExplosionComponent : Component
{

    [DataField("pressureLimit")]
    public float PressureLimit = 1500000f;

    [DataField("explosionType")]
    public string ExplosionPrototype = "FireBomb";

    [DataField("explosionlMultiplier")]
    public float ExplosionMultiplier = 1f;

}
