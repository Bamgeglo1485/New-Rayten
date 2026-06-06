using Content.Shared.Corvax.JoinQueue;
using Robust.Client.Audio;
using Robust.Client.Console;
using Robust.Client.State;
using Robust.Client.UserInterface;
using Robust.Shared.Audio;
using Robust.Shared.Player;


namespace Content.Client.Vanilla.JoinQueue;

public sealed partial class QueueState : State
{
    [Dependency] private IUserInterfaceManager _userInterfaceManager = default!;
    [Dependency] private IClientConsoleHost _consoleHost = default!;

    private readonly SoundSpecifier _joinSound = new SoundPathSpecifier("/Audio/Effects/voteding.ogg");
    private QueueGui? _gui;

    protected override void Startup()
    {
        _gui = new QueueGui();
        _userInterfaceManager.StateRoot.AddChild(_gui);

        _gui.QuitPressed += OnQuitPressed;
    }

    protected override void Shutdown()
    {
        _gui!.QuitPressed -= OnQuitPressed;
        _gui.Dispose();

        Ding();
    }

    private void Ding()
    {
        if (IoCManager.Resolve<IEntityManager>().TrySystem<AudioSystem>(out var audio))
        {
            audio.PlayGlobal(_joinSound, Filter.Local(), false);
        }
    }

    public void OnQueueUpdate(MsgQueueUpdate msg)
    {
        _gui?.UpdateInfo(msg.Total, msg.Position);
    }

    private void OnQuitPressed()
    {
        _consoleHost.ExecuteCommand("quit");
    }
}
