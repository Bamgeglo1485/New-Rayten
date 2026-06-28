using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared.Vanilla.AntagAccept;

[Serializable, NetSerializable]
public sealed class AntagAcceptMessage : EuiMessageBase
{
    public readonly bool Accepted;
    public readonly string? RoleName;

    public AntagAcceptMessage(bool accepted, string? roleName = null)
    {
        Accepted = accepted;
        RoleName = roleName;
    }
}
