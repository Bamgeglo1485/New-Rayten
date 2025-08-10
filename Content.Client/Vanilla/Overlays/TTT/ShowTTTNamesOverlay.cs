using System.Collections.Frozen;
using System.Linq;
using System.Numerics;
using Content.Client.Administration.Systems;
using Content.Client.Stylesheets;
using Content.Shared.Administration;
using Content.Shared.CCVar;
using Content.Shared.Ghost;
using Content.Shared.Mind;
using Content.Shared.Roles;
using Content.Shared.Vanilla.Games.TTT;
using Content.Shared.Examine;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Client.Player;
namespace Content.Client.Vanilla.Overlays;

internal sealed class ShowTTTNamesOverlay : Overlay
{
    private readonly IEntityManager _entityManager;
    private readonly IEyeManager _eyeManager;
    private readonly EntityLookupSystem _entityLookup;
    private readonly IUserInterfaceManager _userInterfaceManager;
    private readonly Font _font;
    private readonly IPlayerManager _playerManager;
    private readonly ExamineSystemShared _examine;


    public ShowTTTNamesOverlay(
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
        _font = resourceCache.NotoStack();
        _playerManager = playerManager;
        _examine = examineSystemShared;
    }

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    protected override void Draw(in OverlayDrawArgs args)
    {
        var viewport = args.WorldAABB;
        var uiScale = _userInterfaceManager.RootControl.UIScale;

        if (!_playerManager.LocalEntity.HasValue)
            return;

        var localPlayer = _playerManager.LocalEntity.Value;

        var query = _entityManager.EntityQueryEnumerator<TTTMarkerComponent>();
        while (query.MoveNext(out var entity, out var marker))
        {
            if ( entity == localPlayer)
                continue;

            if (!_entityManager.EntityExists(entity) || _entityManager.GetComponent<TransformComponent>(entity).MapID != args.MapId)
                continue;

            if (!_examine.InRangeUnOccluded(localPlayer, entity, 12f, ignoreInsideBlocker: false))
                continue;

            var aabb = _entityLookup.GetWorldAABB(entity);
            if (!aabb.Intersects(in viewport))
                continue;

            var screenCoordinatesCenter = _eyeManager.WorldToScreen(aabb.Center).Rounded();

            var textWidth = GetTextWidth(_font, marker.Name, uiScale);

            var centerOffset = new Vector2(-textWidth / 2f, -40f) * uiScale;
            var screenCoordinates = screenCoordinatesCenter + centerOffset;

            var color = Color.Green;
            if (marker.Role == TTTRole.detective)
                color = Color.DodgerBlue;
            args.ScreenHandle.DrawString(_font, screenCoordinates, marker.Name, uiScale, color);
        }
    }

    private float GetTextWidth(Font font, string text, float scale)
    {
        float width = 0f;
        foreach (var rune in text.EnumerateRunes())
        {
            var metrics = font.GetCharMetrics(rune, scale);
            if (metrics.HasValue)
                width += metrics.Value.Advance;
        }
        return width;
    }
}