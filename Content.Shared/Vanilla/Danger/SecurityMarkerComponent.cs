
namespace Content.Shared.Vanilla.Dominator;

[RegisterComponent]
public sealed partial class SecurityMarkerComponent : Component
{
    public TimeSpan? UnFollowOn;

    [DataField]
    public float FollowTime = 15f;

}
