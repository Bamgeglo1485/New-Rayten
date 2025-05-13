using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Vanilla.Background;

[Serializable, NetSerializable, DataDefinition]
public sealed partial class BackGround : IEquatable<BackGround>
{
    [DataField]
    public ProtoId<BackgroundPrototype> Prototype;

    public bool Equals(BackGround? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return Prototype.Equals(other.Prototype);
    }

    public override bool Equals(object? obj)
    {
        return ReferenceEquals(this, obj) || obj is BackGround other && Equals(other);
    }

    public override int GetHashCode()
    {
        return Prototype.GetHashCode();
    }
}