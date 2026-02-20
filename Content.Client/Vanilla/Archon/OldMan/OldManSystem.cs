using Content.Shared.Vanilla.Archon.OldMan;
using Robust.Client.Animations;
using Robust.Client.Graphics;
using Robust.Client.GameObjects;
using System.Numerics;
namespace Content.Client.Vanilla.Archon.OldMan;

public sealed class OldManSystem : SharedOldManSystem
{
    [Dependency] private readonly AnimationPlayerSystem _player = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<OldManFoodComponent, MapInitEvent>(sperma);
    }
    private void sperma(EntityUid uid, OldManFoodComponent comp, ref MapInitEvent args)
    {
        if (timing.IsFirstTimePredicted)
            Fall(uid);
    }



    private static readonly Animation FallAnimation = new()
    {
        Length = TimeSpan.FromSeconds(1.15f),
        AnimationTracks =
        {
            // 🕳 Падение
            new AnimationTrackComponentProperty
            {
                ComponentType = typeof(SpriteComponent),
                Property = nameof(SpriteComponent.Offset),
                KeyFrames =
                {
                    new(new Vector2(0f, 22f), 0f),     // внутри потолка
                    new(new Vector2(0f, 20f), 0.2f),   // завис, будто реальность рвётся
                    new(new Vector2(0f, -3f), 0.65f),  // резкий прорыв вниз
                    new(new Vector2(0f, 1.2f), 0.9f),  // лёгкий отскок
                    new(Vector2.Zero, 1.15f),          // стабилизация
                }
            },
            // 🧱 Масса (squash & stretch)
            new AnimationTrackComponentProperty
            {
                ComponentType = typeof(SpriteComponent),
                Property = nameof(SpriteComponent.Scale),
                KeyFrames =
                {
                    new(new Vector2(1f, 0.45f), 0f),    // сплющен в потолке
                    new(new Vector2(1f, 1.35f), 0.65f), // растянут от удара
                    new(new Vector2(1f, 0.95f), 0.9f),
                    new(new Vector2(1f, 1f), 1.15f),
                }
            },
            // 🌑 Проявление из тьмы
            new AnimationTrackComponentProperty
            {
                ComponentType = typeof(SpriteComponent),
                Property = nameof(SpriteComponent.Color),
                KeyFrames =
                {
                    new(new Color(0f, 0f, 0f, 0f), 0f),          // невидим
                    new(new Color(0.1f, 0.1f, 0.1f, 0.8f), 0.4f),
                    new(new Color(1f, 1f, 1f, 1f), 0.8f),        // полностью проявился
                }
            },
            // 🌀 Лёгкая нестабильность
            new AnimationTrackComponentProperty
            {
                ComponentType = typeof(SpriteComponent),
                Property = nameof(SpriteComponent.Rotation),
                KeyFrames =
                {
                    new(Angle.FromDegrees(-6), 0f),
                    new(Angle.FromDegrees(4), 0.6f),
                    new(Angle.Zero, 1.15f),
                }
            }
        }
    };

    protected override void Fall(EntityUid uid)
    {
        Log.Info("делаем анимцию");
        _player.Play(uid, FallAnimation, "fall-animation");
    }
}

