using Content.Shared.Examine;
using Content.Shared.Vanilla.Games.TTT;
using Content.Client.Vanilla.TTT.Overlays;
using Robust.Shared.Player;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.ResourceManagement;
using Robust.Client.Player;

namespace Content.Client.Vanilla.Games.TTT;

public sealed class TTTSystem : SharedTTTSystem
{
    [Dependency] private readonly IOverlayManager _overlay = default!;
    [Dependency] private readonly IEyeManager _eyeManager = default!;
    [Dependency] private readonly EntityLookupSystem _entityLookup = default!;
    [Dependency] private readonly IUserInterfaceManager _userInterfaceManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly ExamineSystemShared _examineSystemShared = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<NameOverlayComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<NameOverlayComponent, ComponentShutdown>(OnShutDown);
        SubscribeLocalEvent<NameOverlayComponent, LocalPlayerAttachedEvent>(OnAttched);
        SubscribeLocalEvent<NameOverlayComponent, LocalPlayerDetachedEvent>(OnDetached);
    }
    private void OnAttched(EntityUid uid, NameOverlayComponent comp, LocalPlayerAttachedEvent args)
    {
        _overlay.AddOverlay(new ShowNamesOverlay(EntityManager, _eyeManager, IoCManager.Resolve<IResourceCache>(), _entityLookup, _userInterfaceManager, _playerManager, _examineSystemShared));
    }
    private void OnDetached(EntityUid uid, NameOverlayComponent comp, LocalPlayerDetachedEvent args)
    {
        _overlay.RemoveOverlay<ShowNamesOverlay>();
    }
    private void OnStartup(EntityUid uid, NameOverlayComponent comp, ComponentStartup args)
    {
        if (!_playerManager.LocalEntity.HasValue)
            return;
        if (uid == _playerManager.LocalEntity.Value)
            _overlay.AddOverlay(new ShowNamesOverlay(EntityManager, _eyeManager, IoCManager.Resolve<IResourceCache>(), _entityLookup, _userInterfaceManager, _playerManager, _examineSystemShared));
    }

    private void OnShutDown(EntityUid uid, NameOverlayComponent comp, ComponentShutdown args)
    {
        if (!_playerManager.LocalEntity.HasValue)
            return;
        if (uid == _playerManager.LocalEntity.Value)
            _overlay.RemoveOverlay<ShowNamesOverlay>();
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlay.RemoveOverlay<ShowNamesOverlay>();
    }
}
