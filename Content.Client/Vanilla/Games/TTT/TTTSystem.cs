using Content.Shared.Vanilla.Games.TTT;
using Content.Client.Vanilla.TTT.Overlays;
using Robust.Shared.Player;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.GameObjects;

namespace Content.Client.Vanilla.Games.TTT;

public sealed partial class TTTSystem : SharedTTTSystem
{
    [Dependency] private IOverlayManager _overlay = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private SpriteSystem _sprite = default!;

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
        _overlay.AddOverlay(new ShowNamesOverlay());
    }

    private void OnDetached(EntityUid uid, NameOverlayComponent comp, LocalPlayerDetachedEvent args)
    {
        _overlay.RemoveOverlay<ShowNamesOverlay>();
        var query = EntityQueryEnumerator<NameOverlayComponent, SpriteComponent>();
        while (query.MoveNext(out var entity, out _, out _))
            _sprite.SetVisible(entity, true);
    }

    private void OnStartup(EntityUid uid, NameOverlayComponent comp, ComponentStartup args)
    {
        if (!_playerManager.LocalEntity.HasValue)
            return;
        if (uid == _playerManager.LocalEntity.Value)
            _overlay.AddOverlay(new ShowNamesOverlay());
    }

    private void OnShutDown(EntityUid uid, NameOverlayComponent comp, ComponentShutdown args)
    {
        if (!_playerManager.LocalEntity.HasValue)
            return;

        if (uid != _playerManager.LocalEntity.Value)
            return;

        _overlay.RemoveOverlay<ShowNamesOverlay>();
        var query = EntityQueryEnumerator<NameOverlayComponent, SpriteComponent>();
        while (query.MoveNext(out var entity, out _, out _))
            _sprite.SetVisible(entity, true);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlay.RemoveOverlay<ShowNamesOverlay>();
    }
}
