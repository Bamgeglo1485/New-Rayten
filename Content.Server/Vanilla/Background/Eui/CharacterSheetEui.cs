using Content.Server.EUI;
using Content.Shared.Eui;
using Content.Shared.Vanilla.Background.eui;

namespace Content.Server.Vanilla.Background.eui;

public sealed class CharacterSheetEui : BaseEui
{
    private readonly NetEntity _target;

    public CharacterSheetEui(NetEntity target)
    {
        _target = target;
    }

    public override void Opened()
    {
        StateDirty();
    }

    public override EuiStateBase GetNewState()
    {
        return new CharacterSheetEuiState(_target);
    }
}
