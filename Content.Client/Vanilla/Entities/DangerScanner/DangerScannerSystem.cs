using Content.Shared.Contraband;
using Content.Shared.Vanilla.Entities.DangerScanner;
using Robust.Client.GameObjects;
using Robust.Client.Animations;
using Robust.Client.Graphics;

namespace Content.Client.Vanilla.Entities.DangerScanner;

public sealed partial class DangerScannerSystem : SharedDangerScannerSystem
{
    private const string ScannerAnimationKey = "danger-scan";
    [Dependency] private AnimationPlayerSystem _animation = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DangerScannerComponent, AnimationCompletedEvent>(OnAnimationCompleted);
    }
    /// <summary>
    /// Запускает анимацию DangerScanner для указанного слоя.
    /// </summary>
    protected override void PlayScanAnimation(EntityUid uid, string scanLayer)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        // Проверка, чтобы не перезапускать текущую анимацию
        if (_animation.HasRunningAnimation(uid, ScannerAnimationKey))
            return;

        if (sprite.BaseRSI == null)
            return;

        // Получаем состояние RSI для слоя
        if (!sprite.BaseRSI.TryGetState(scanLayer.ToLower(), out var state))
            return;

        var anim = new Animation()
        {
            Length = TimeSpan.FromSeconds(state.AnimationLength),
            AnimationTracks =
            {
                new AnimationTrackSpriteFlick()
                {
                    LayerKey = DangerScannerLayers.Mode,
                    KeyFrames =
                    {
                        new AnimationTrackSpriteFlick.KeyFrame(scanLayer.ToLower(), 0f)
                    }
                }
            }
        };
        _animation.Play(uid, anim, ScannerAnimationKey);
    }

    private void OnAnimationCompleted(Entity<DangerScannerComponent> ent, ref AnimationCompletedEvent args)
    {
        if (args.Key != ScannerAnimationKey)
            return;

        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        _sprite.LayerSetRsiState((ent.Owner, sprite), DangerScannerLayers.Mode, new RSI.StateId("scanning"));
        _sprite.LayerSetAutoAnimated((ent.Owner, sprite), DangerScannerLayers.Mode, true);
    }

    //server-only
    protected override void SetWanted(EntityUid scanner, DangerScannerComponent component, string target, EntityUid item, ContrabandComponent contraband)
    {
    }
}
