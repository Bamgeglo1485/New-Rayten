namespace Content.Shared.Vanilla.Entities.SecuritronWhistle;

[RegisterComponent]
public sealed partial class SecuritronMasterComponent : Component
{
    public TimeSpan? UnFollowOn;

    [DataField]
    public float FollowTime = 15f;

    [DataField]
    public HashSet<EntityUid> Securitrons = new();
}
