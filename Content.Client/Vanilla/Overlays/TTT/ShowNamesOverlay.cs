using System.Numerics;
using Content.Client.Stylesheets;
using Content.Shared.Vanilla.Games.TTT;
using Content.Shared.Examine;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Enums;
using Robust.Client.Player;
using Robust.Client.GameObjects;
namespace Content.Client.Vanilla.TTT.Overlays;

public sealed class ShowNamesOverlay : Overlay
{
    private readonly IEntityManager _entityManager;
    private readonly IEyeManager _eyeManager;
    private readonly EntityLookupSystem _entityLookup;
    private readonly IUserInterfaceManager _userInterfaceManager;
    private readonly Font _font;
    private readonly IPlayerManager _playerManager;
    private readonly ExamineSystemShared _examine;
    private readonly SharedTransformSystem _transformSystem;
    private readonly SpriteSystem _sprite;
    public ShowNamesOverlay(
        IEntityManager entityManager,
        IEyeManager eyeManager,
        IResourceCache resourceCache,
        EntityLookupSystem entityLookup,
        IUserInterfaceManager userInterfaceManager,
        IPlayerManager playerManager,
        ExamineSystemShared examineSystemShared
        )
    {
        _entityManager = entityManager;
        _eyeManager = eyeManager;
        _entityLookup = entityLookup;
        _userInterfaceManager = userInterfaceManager;
        _font = resourceCache.NotoStack(size: 12);
        _playerManager = playerManager;
        _examine = examineSystemShared;
        _transformSystem = _entityManager.System<SharedTransformSystem>();
        _sprite = _entityManager.System<SpriteSystem>();
    }

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;
    protected override void Draw(in OverlayDrawArgs args)
    {
        var viewport = args.WorldAABB;
        var uiScale = _userInterfaceManager.RootControl.UIScale;

        if (!_playerManager.LocalEntity.HasValue)
            return;

        var localPlayer = _playerManager.LocalEntity.Value;

        var playerPos = _transformSystem.GetWorldPosition(localPlayer);
        var playerForward = _transformSystem.GetWorldRotation(localPlayer).ToWorldVec();

        var query = _entityManager.EntityQueryEnumerator<NameOverlayComponent, SpriteComponent, TransformComponent>();
        while (query.MoveNext(out var entity, out var marker, out var sprite, out var xform))
        {
            if (entity == localPlayer)
                continue;

            if (xform.MapID != args.MapId)
                continue;

            if (!_examine.InRangeUnOccluded(localPlayer, entity, 12f, ignoreInsideBlocker: false))
                continue;

            var entityPos = _transformSystem.GetWorldPosition(entity);
            var dirToEntity = entityPos - playerPos;

            var dirNorm = dirToEntity.Normalized();
            var dot = Vector2.Dot(playerForward, dirNorm);

            if (dot <= 0f)
            {
                _sprite.SetVisible(entity, false);
                continue;
            }
            _sprite.SetVisible(entity, true);

            var aabb = _entityLookup.GetWorldAABB(entity);
            if (!aabb.Intersects(in viewport))
                continue;

            var screenCoordinatesCenter = _eyeManager.WorldToScreen(aabb.Center).Rounded();

            var textWidth = GetTextWidth(_font, marker.Name, uiScale);

            var centerOffset = new Vector2(-textWidth / 2f, -40f) * uiScale;
            var screenCoordinates = screenCoordinatesCenter + centerOffset;

            args.ScreenHandle.DrawString(_font, screenCoordinates, marker.Name, uiScale, marker.NameColor);
        }
    }
    private float GetTextWidth(Font font, string text, float scale)
    {
        var width = 0f;
        foreach (var rune in text.EnumerateRunes())
        {
            var metrics = font.GetCharMetrics(rune, scale);
            if (metrics.HasValue)
                width += metrics.Value.Advance;
        }
        return width;
    }
}
