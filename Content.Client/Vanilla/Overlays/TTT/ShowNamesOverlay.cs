
using Content.Client.Stylesheets;
using Content.Shared.Vanilla.Games.TTT;
using Content.Shared.Examine;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Enums;
using Robust.Client.Player;
using Robust.Client.GameObjects;
using Robust.Client.Input;
using System.Numerics;

namespace Content.Client.Vanilla.TTT.Overlays;

public sealed partial class ShowNamesOverlay : Overlay
{
    const float ViewConeDot = 0.6428f;//угол взгляда 0.6428f ~100градусов

    [Dependency] private IInputManager _input = default!;
    [Dependency] private IEntityManager _entityManager = default!;
    [Dependency] private IEyeManager _eyeManager = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IUserInterfaceManager _userInterfaceManager = default!;
    private readonly EntityLookupSystem _entityLookup;
    private readonly Font _font;
    private readonly ExamineSystemShared _examine;
    private readonly SharedTransformSystem _transformSystem;
    private readonly SpriteSystem _sprite;
    public ShowNamesOverlay()
    {
        IoCManager.InjectDependencies(this);
        _font = IoCManager.Resolve<IResourceCache>().NotoStack(size: 13);
        _transformSystem = _entityManager.System<SharedTransformSystem>();
        _sprite = _entityManager.System<SpriteSystem>();
        _entityLookup = _entityManager.System<EntityLookupSystem>();
        _examine = _entityManager.System<ExamineSystemShared>();
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

        // направление взгляда мышки
        var mouseScreen = _input.MouseScreenPosition;
        var mouseWorld = _eyeManager.ScreenToMap(mouseScreen).Position;
        var lookDir = (mouseWorld - playerPos).Normalized();

        // квадрат вокруг курсора
        var mousePos = _eyeManager.PixelToMap(mouseScreen);
        var expansion = new Vector2(0.5f, 0.5f);
        var bounds = new Box2(mousePos.Position - expansion, mousePos.Position + expansion);

        var mouseEntities = new HashSet<EntityUid>(_entityLookup.GetEntitiesIntersecting(mousePos.MapId, bounds));

        var query = _entityManager.EntityQueryEnumerator<NameOverlayComponent, SpriteComponent, TransformComponent>();

        while (query.MoveNext(out var entity, out var marker, out var sprite, out var xform))
        {
            if (entity == localPlayer)
                continue;

            if (xform.MapID != args.MapId)
                continue;

            if (!_examine.InRangeUnOccluded(localPlayer, entity, 30f))
                continue;

            var entityPos = _transformSystem.GetWorldPosition(entity);
            var dirToEntity = entityPos - playerPos;

            var dirNorm = dirToEntity.Normalized();
            var dot = Vector2.Dot(lookDir, dirNorm);

            // FOV влияет только на спрайт
            if (dot <= ViewConeDot)
            {
                _sprite.SetVisible(entity, false);
                continue;
            }
            _sprite.SetVisible(entity, true);

            // ник показываем только если сущность в квадрате курсора
            if (!mouseEntities.Contains(entity))
                continue;

            var aabb = _entityLookup.GetWorldAABB(entity);
            if (!aabb.Intersects(in viewport))
                continue;

            var screenCoordinatesCenter = _eyeManager.WorldToScreen(aabb.Center).Rounded();

            var textWidth = GetTextWidth(_font, marker.Name, uiScale);
            var centerOffset = new Vector2(-textWidth / 2f, -50f) * uiScale;
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
