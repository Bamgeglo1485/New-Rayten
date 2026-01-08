
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
    [Dependency] protected readonly IEntityManager Entity = default!;
    [Dependency] protected readonly IPlayerManager PlayerManager = default!;

    protected float ShowRadius;

    private readonly EntityLookupSystem _entityLookup;
    private readonly TransformSystem _transformSystem;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public BlindPredatorOverlay(float showRadius)
    {
        IoCManager.InjectDependencies(this);
        _entityLookup = Entity.System<EntityLookupSystem>();
        _transformSystem = Entity.System<TransformSystem>();
        ShowRadius = showRadius;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (PlayerManager.LocalEntity == null)
            return;
        var user = PlayerManager.LocalEntity.Value;
        if (!Entity.TryGetComponent<TransformComponent>(user, out var playerTransform))
            return;

        var handle = args.WorldHandle;
        var eye = args.Viewport.Eye;
        var eyeRot = eye?.Rotation ?? default;

        var entities = _entityLookup.GetEntitiesInRange<PredatorVisibleMarkComponent>(playerTransform.Coordinates, ShowRadius);
        foreach (var (uid, comp) in entities)
        {
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
        sprite.Color = Color.Red;
        sprite.Render(handle, eyeRot, rotation, position: position);
        sprite.Color = oldColor;
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

        if (!Entity.TryGetComponent<SpriteComponent>(target, out sprite))
            return true;
        if (!Entity.TryGetComponent<TransformComponent>(target, out xform))
            return true;

        return false;
    }
}