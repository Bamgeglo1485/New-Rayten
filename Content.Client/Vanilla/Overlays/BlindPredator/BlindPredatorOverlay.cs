
using Content.Shared.Vanilla.Archon.BlindPredator;
using Robust.Client.GameObjects;
using Robust.Shared.Enums;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Map;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;

namespace Content.Client.Vanilla.Overlays.BlindPredator;

public sealed class BlindPredatorOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entity = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    private readonly float _showRadius;
    private readonly EntityLookupSystem _entityLookup;
    private readonly TransformSystem _transformSystem;
    private readonly SpriteSystem _sprite;
    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public BlindPredatorOverlay(float showRadius)
    {
        IoCManager.InjectDependencies(this);
        _entityLookup = _entity.System<EntityLookupSystem>();
        _transformSystem = _entity.System<TransformSystem>();
        _sprite = _entity.System<SpriteSystem>();
        _showRadius = showRadius;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_playerManager.LocalEntity == null)
            return;
        var user = _playerManager.LocalEntity.Value;
        if (!_entity.TryGetComponent<TransformComponent>(user, out var playerTransform))
            return;

        var handle = args.WorldHandle;
        var eye = args.Viewport.Eye;
        var eyeRot = eye?.Rotation ?? default;

        var entities = _entityLookup.GetEntitiesInRange<PredatorVisibleMarkComponent>(playerTransform.Coordinates, _showRadius);
        foreach (var (uid, comp) in entities)
        {
            if (uid == user)
                continue;

            if (CantBeRendered(uid, out var sprite, out var xform))
                continue;

            if (!CanBeSeen(user, comp))
                continue;

            Render((uid, sprite, xform), eye?.Position.MapId, handle, eyeRot);
        }
        handle.SetTransform(Matrix3x2.Identity);
    }

    private void Render(Entity<SpriteComponent, TransformComponent> ent, MapId? map, DrawingHandleWorld handle, Angle eyeRot)
    {
        var (_, sprite, xform) = ent;
        if (xform.MapID != map)
            return;

        var position = _transformSystem.GetWorldPosition(xform);
        var rotation = _transformSystem.GetWorldRotation(xform);

        var oldColor = sprite.Color;
        _sprite.SetColor((ent.Owner, sprite), Color.Red);
        _sprite.RenderSprite((ent.Owner, sprite), handle, eyeRot, rotation, position, null);
        _sprite.SetColor((ent.Owner, sprite), oldColor);
    }

    private bool CanBeSeen(EntityUid user, PredatorVisibleMarkComponent comp)
    {
        if (!comp.Predators.TryGetValue(user, out var val))
            return false;

        return val;
    }

    private bool CantBeRendered(EntityUid target, [NotNullWhen(false)] out SpriteComponent? sprite,
                                                [NotNullWhen(false)] out TransformComponent? xform)
    {
        sprite = null;
        xform = null;

        if (!_entity.TryGetComponent<SpriteComponent>(target, out sprite))
            return true;
        if (!_entity.TryGetComponent<TransformComponent>(target, out xform))
            return true;

        return false;
    }
}
