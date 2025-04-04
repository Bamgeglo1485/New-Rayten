using Content.Shared.Mind;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Player;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Content.Shared.Vanilla.Background;
using Content.Client.Vanilla.UserInterface.GhostBackground;
namespace Content.Client.Vanilla.Background;

public sealed class BackgroundSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IUserInterfaceManager _userInterfaceManager = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AwaitBackgroundComponent, LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<AwaitBackgroundComponent, ComponentShutdown>(OnComponentShutdown);
    }
    private void OnComponentShutdown(EntityUid uid, AwaitBackgroundComponent component, ComponentShutdown args)
    {
        if (_player.LocalSession?.AttachedEntity == uid)
            _userInterfaceManager.GetUIController<GhostBackgroundUIController>().CloseWindow();
    }
    private void OnPlayerAttached(EntityUid uid, AwaitBackgroundComponent component, LocalPlayerAttachedEvent args)
    {
        if (component.BackgroundGroup == null )
            return;

        _userInterfaceManager.GetUIController<GhostBackgroundUIController>().createbackground(component.BackgroundGroup.Value);
    }
    public void TakeGhostBackground(ProtoId<BackgroundPrototype> background)
    {
        RaiseNetworkEvent(new TakeGhostBackgroundEvent(background));
    }
}