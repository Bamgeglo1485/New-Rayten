using Content.Shared.Eui;
using Robust.Shared.Serialization;
using Robust.Shared.Network;

namespace Content.Shared.Vanilla.Background.eui;

[NetSerializable, Serializable]
public sealed class CharacterSheetEuiState : EuiStateBase
{
    public NetEntity Target { get; }

    public CharacterSheetEuiState(NetEntity target)
    {
        Target = target;
    }
}
