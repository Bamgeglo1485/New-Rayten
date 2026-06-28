using Content.Client.Eui;
using Content.Client.Ghost.UI;
using Content.Shared.Eui;
using Content.Shared.Vanilla.AntagAccept;
using JetBrains.Annotations;
using Robust.Client.Graphics;

namespace Content.Client.Vanilla.AntagAccept.UI;

[UsedImplicitly]
public sealed class AntagAcceptEui : BaseEui
{
    private readonly AntagAcceptMenu _menu;
    private string? _roleName;

    public AntagAcceptEui()
    {
        _menu = new AntagAcceptMenu();

        _menu.DenyButton.OnPressed += _ =>
        {
            SendMessage(new AntagAcceptMessage(false));
            _menu.Close();
        };

        _menu.AcceptButton.OnPressed += _ =>
        {
            SendMessage(new AntagAcceptMessage(true));
            _menu.Close();
        };
    }

    public override void Opened()
    {
        IoCManager.Resolve<IClyde>().RequestWindowAttention();
        _menu.OpenCentered();
    }

    public override void HandleState(EuiStateBase state)
    {
        base.HandleState(state);

        if (state is AntagAcceptEuiState antagState)
        {
            _roleName = antagState.RoleName;
            _menu.UpdateRoleName(antagState.RoleName);
        }
    }

    public override void Closed()
    {
        base.Closed();
        SendMessage(new AntagAcceptMessage(false));
        _menu.Close();
    }
}
