using Content.Server.EUI;
using Content.Shared.Eui;
using Content.Shared.Vanilla.AntagAccept;
using Robust.Shared.Player;
using Content.Shared.Antag;
using Content.Server.Antag;
using Robust.Shared.IoC;
using Robust.Shared.GameObjects;

namespace Content.Server.Vanilla.AntagAccept;

public sealed class AntagAcceptEui : BaseEui
{
    private readonly AntagSpecifierPrototype _antag;
    private readonly string _roleName;
    private readonly ICommonSession _session;
    private readonly AntagSelectionSystem _antagSystem;

    public AntagAcceptEui(AntagSpecifierPrototype antag, string roleName, ICommonSession session)
    {
        _antag = antag;
        _roleName = roleName;
        _session = session;

        var entityManager = IoCManager.Resolve<IEntityManager>();
        _antagSystem = entityManager.EntitySysManager.GetEntitySystem<AntagSelectionSystem>();
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        if (msg is not AntagAcceptMessage choice)
        {
            Close();
            return;
        }

        _antagSystem.OnAntagAcceptMessage(choice, _session);
        Close();
    }

    public override EuiStateBase GetNewState()
    {
        return new AntagAcceptEuiState(_roleName);
    }
}
