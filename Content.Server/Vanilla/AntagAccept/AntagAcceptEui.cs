using Content.Server.EUI;
using Content.Shared.Eui;
using Content.Shared.Vanilla.AntagAccept;
using Content.Shared.Mind;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Content.Shared.Antag;
using Content.Server.Antag;

namespace Content.Server.Vanilla.AntagAccept;

public sealed class AntagAcceptEui : BaseEui
{
    private readonly AntagSpecifierPrototype _antag;
    private readonly string _roleName;
    private readonly ICommonSession _session;

    public AntagAcceptEui(AntagSpecifierPrototype antag, string roleName, ICommonSession session)
    {
        _antag = antag;
        _roleName = roleName;
        _session = session;
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        if (msg is not AntagAcceptMessage choice)
        {
            Close();
            return;
        }

        var system = IoCManager.Resolve<AntagSelectionSystem>();
        system.OnAntagAcceptMessage(choice, _session);

        Close();
    }

    public override EuiStateBase GetNewState()
    {
        return new AntagAcceptEuiState(_roleName);
    }
}
