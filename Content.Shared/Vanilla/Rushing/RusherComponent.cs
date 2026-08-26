namespace Content.Shared.Vanilla.Rushing;

[RegisterComponent]
public sealed partial class RusherComponent : Component
{
    [DataField]
    public float StaminaLoss = 25f;

    [DataField]
    public float Speed = 6f;

    [DataField]
    public float DistanceModifier = 0.35f;
}
