
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs;
using Robust.Client.GameObjects;
using Robust.Shared.Enums;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Map;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;

namespace Content.Client.Vanilla.Overlays.ThermalVision;

public sealed class ThermalVisionOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entity = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    private readonly float _showRadius;

    private readonly EntityLookupSystem _entityLookup;
    private readonly TransformSystem _transformSystem;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public ThermalVisionOverlay(float showRadius)
    {
        IoCManager.InjectDependencies(this);
        _entityLookup = _entity.System<EntityLookupSystem>();
        _transformSystem = _entity.System<TransformSystem>();
        _showRadius = showRadius;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_playerManager.LocalEntity == null)
            return;

        if (!_entity.TryGetComponent<TransformComponent>(_playerManager.LocalEntity, out var playerTransform))
            return;

        var handle = args.WorldHandle;
        var eye = args.Viewport.Eye;
        var eyeRot = eye?.Rotation ?? default;

        var entities = _entityLookup.GetEntitiesInRange<MobStateComponent>(playerTransform.Coordinates, _showRadius);
        foreach (var (uid, stateComp) in entities)
        {
            if (CantBeRendered(uid, out var sprite, out var xform))
                continue;

            if (CantBeSeen((uid, stateComp)))
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
        sprite.Color = Color.Orange;
        sprite.Render(handle, eyeRot, rotation, position: position);
        sprite.Color = oldColor;
    }
    /// <summary>
    ///  Если сущность мертва или какая-та паранольмальная (не может умереть, не может быть живой) - то true
    /// </summary>
    private bool CantBeSeen(Entity<MobStateComponent> target)
    {
        var states = target.Comp.AllowedStates;

        if (target.Comp.CurrentState == MobState.Dead)
            return true;

        if (states.Contains(MobState.Dead) &&
            states.Contains(MobState.Alive))
            return false;

        return true;
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
