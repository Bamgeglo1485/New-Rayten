using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared.Vanilla.AntagAccept;

[Serializable, NetSerializable]
public sealed class AntagAcceptEuiState : EuiStateBase
{
    public readonly string RoleName;

    public AntagAcceptEuiState(string roleName)
    {
        RoleName = roleName;
    }
}
