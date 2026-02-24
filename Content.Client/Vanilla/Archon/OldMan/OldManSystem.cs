using Content.Shared.Vanilla.Archon.OldMan;
using Robust.Shared.Prototypes;
using Robust.Client.Animations;
using Robust.Client.Graphics;
using Robust.Client.GameObjects;
using System.Numerics;
namespace Content.Client.Vanilla.Archon.OldMan;

public sealed class OldManSystem : SharedOldManSystem
{
    private static readonly ProtoId<ShaderPrototype> StencilEqualDrawShader = "StencilEqualDraw";
    [Dependency] private readonly AnimationPlayerSystem _animationPlayer = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly IOverlayManager _overlayMan = default!;
    [Dependency] private readonly IPrototypeManager _protoMan = default!;
    private PuddleMaskOverlay _overlay = default!;
    private ShaderInstance _shader = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<FallAnimationEvent>(OnFallAnimation);
        SubscribeLocalEvent<AnimationCompletedEvent>(OnFallAnimationComplete);

        SubscribeLocalEvent<PDAnimationComponent, AnimationCompletedEvent>(OnAnimationComplete);
        SubscribeLocalEvent<PDAnimationComponent, BeforePostShaderRenderEvent>(OnShaderRender);
        SubscribeLocalEvent<PDAnimationComponent, AfterAutoHandleStateEvent>(OnHandleState);
        SubscribeLocalEvent<PDAnimationComponent, ComponentShutdown>(OnPDShutdown);
        _overlay = new();
        _overlayMan.AddOverlay(_overlay);
        _shader = _protoMan.Index(StencilEqualDrawShader).InstanceUnique();
    }
    public override void Shutdown()
    {
        base.Shutdown();
        _overlayMan.RemoveOverlay<PuddleMaskOverlay>();
    }
    #region анимация входа/выхода в портал
    private void OnShaderRender(EntityUid uid, PDAnimationComponent component, BeforePostShaderRenderEvent args)
    {
        if (!TryComp(uid, out SpriteComponent? sprite))
            return;

        _sprite.SetOffset(uid, component.InsertOffset);
    }

    private void OnHandleState(EntityUid uid, PDAnimationComponent comp, AfterAutoHandleStateEvent args)
    {
        TryStartAnimation(uid, comp);
    }
    private void OnPDShutdown(EntityUid uid, PDAnimationComponent comp, ComponentShutdown args)
    {
        TryStopAnimation(uid);
    }
    protected override void TryStopAnimation(EntityUid uid)
    {
        if (_animationPlayer.HasRunningAnimation(uid, "insert-pd-animation"))
            _animationPlayer.Stop(uid, "insert-pd-animation");
    }

    protected override void TryStartAnimation(EntityUid uid, PDAnimationComponent comp)
    {
        TryStopAnimation(uid);
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;
        comp.PuddleEntity = Spawn("PDEnterPortal", Transform(uid).Coordinates);
        sprite.PostShader = _shader;
        sprite.RaiseShaderEvent = true;

        // Запускаем анимацию, которая двигает спрайт вниз
        var animation = CreatePDAnimation(comp.TeleportDuration, comp.IsOut);
        _animationPlayer.Play(uid, animation, "insert-pd-animation");
    }
    private Animation CreatePDAnimation(float duration, bool isOut)
    {
        duration += 0.35f;
        var start = isOut ? new Vector2(0f, -1f) : Vector2.Zero;
        var end = isOut ? Vector2.Zero : new Vector2(0f, -1f);

        return new Animation
        {
            Length = TimeSpan.FromSeconds(duration),
            AnimationTracks =
        {
            new AnimationTrackComponentProperty
            {
                ComponentType = typeof(PDAnimationComponent),
                Property = nameof(PDAnimationComponent.InsertOffset),
                KeyFrames =
                {
                    new(start, 0f),
                    new(start, 1f),
                    new(end, 1.5f),
                    new(end, 0.35f),
                }
            }
        }
        };
    }
    private void OnAnimationComplete(EntityUid uid, PDAnimationComponent comp, ref AnimationCompletedEvent args)
    {
        if (args.Key != "insert-pd-animation")
            return;

        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;
        _sprite.SetOffset(uid, Vector2.Zero);
        _sprite.SetVisible(uid, true);
        sprite.RaiseShaderEvent = false;
        sprite.PostShader = null;
        if (!Deleted(comp.PuddleEntity))
            Del(comp.PuddleEntity);
    }
    #endregion
    #region анимция падения
    private void OnFallAnimation(FallAnimationEvent ev)
    {
        var uid = GetEntity(ev.Target);
        TryStopAnimation(uid);
        if (!_animationPlayer.HasRunningAnimation(uid, "fall-animation"))
            _animationPlayer.Play(uid, FallAnimation, "fall-animation");
    }
    private static readonly Animation FallAnimation = new()
    {
        Length = TimeSpan.FromSeconds(1.7f),
        AnimationTracks =
        {
            // 🕳 Падение
            new AnimationTrackComponentProperty
            {
                ComponentType = typeof(SpriteComponent),
                Property = nameof(SpriteComponent.Offset),
                KeyFrames =
                {
                    new(new Vector2(0f, 5f), 0f),      // старт
                    new(new Vector2(0f, -0.3f), 0.8f),  // падение
                    new(new Vector2(0f, 0.3f), 0.3f),   // отскок
                    new(Vector2.Zero, 0.3f),   // стабилизация
                }
            }
        }
    };
    private void OnFallAnimationComplete(AnimationCompletedEvent ev)
    {
        if (ev.Key != "fall-animation")
            return;
        _sprite.SetOffset(ev.Uid, Vector2.Zero);
        _sprite.SetVisible(ev.Uid, true);
    }
    #endregion
}
