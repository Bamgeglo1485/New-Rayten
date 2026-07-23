using Robust.Shared.GameObjects;

namespace Content.Shared.Vanilla.SoulTie;

[RegisterComponent]
public sealed partial class SoulTiedComponent : Component
{
    [DataField]
    public EntityUid? Another;

    [DataField]
    public SoulTiedComponent? AnotherSoulTied;

    [DataField]
    public bool Damaged;
}
