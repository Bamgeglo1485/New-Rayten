using Robust.Shared.Timing;
using Content.Shared.Audio; 
using Content.Shared.Chat;
using Robust.Shared.Random;
using Robust.Shared.Audio;
using System.Text.RegularExpressions;
using Robust.Shared.Audio.Systems;
using System.Linq;
using Content.Shared.Corvax.CCCVars;
using Content.Client.Examine;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;
using Robust.Client.Player;
using Robust.Client.GameObjects;
using Robust.Shared.Map;

namespace Content.Client.Vanilla.UndertaleSpeech;

public sealed class UndertaleSpeechSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IEyeManager _eyeManager = default!;
    [Dependency] private readonly ExamineSystem? _examine = default;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly TransformSystem? _transform = default;
    private float _volume = 0.0f;

    public override void Initialize()
    {
        _cfg.OnValueChanged(CCCVars.TTSVolume, OnTtsVolumeChanged, true);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _cfg.UnsubValueChanged(CCCVars.TTSVolume, OnTtsVolumeChanged);
    }

    private void OnTtsVolumeChanged(float volume)
    {
        _volume = volume;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;

        if (_examine == null)
            return;

        foreach (var comp in EntityQuery<UndertaleSpeechComponent>())
        {
            var uid = comp.Owner;
            var player = _player.LocalEntity;

            if (comp.RemainingText == null || comp.Sound == null)
            {
                RemCompDeferred<UndertaleSpeechComponent>(uid);
                continue;
            }

            if (now < comp.NextBeepTime)
                continue;

            var predicate = static (EntityUid uid, (EntityUid compOwner, EntityUid? attachedEntity) data)
                => uid == data.compOwner || uid == data.attachedEntity;

            var occluded = player != null && _examine.IsOccluded(player.Value);

            var playerPos = player != null
                ? _eyeManager.CurrentEye.Position
                : MapCoordinates.Nullspace;

            var otherPos = _transform?.GetMapCoordinates(uid) ?? MapCoordinates.Nullspace;

            if (occluded && !_examine.InRangeUnOccluded(
                    playerPos,
                    otherPos, 0f,
                    (uid, player), predicate))
            {
                comp.RemainingText = comp.RemainingText[1..];
                comp.NextBeepTime = now + TimeSpan.FromSeconds(AdjustWordSpacing(comp.RemainingText));
                continue;
            }

            // Пропускаем не буквенно-цифровые символы
            comp.RemainingText = Regex.Replace(comp.RemainingText, @"[^a-zA-Z0-9а-яА-ЯёЁ\s!?]", string.Empty);

            if (comp.RemainingText.Length == 0)
            {
                RemCompDeferred<UndertaleSpeechComponent>(uid);
                continue;
            }

            if (comp.RemainingText[0] == ' ')
            {
                comp.NextBeepTime = now + TimeSpan.FromSeconds(0.1f);
                comp.RemainingText = comp.RemainingText[1..];
                continue;
            }

            var sound = comp.Sound;

            sound.Params = AudioParams.Default.WithPitchScale(AdjustPitchBasedOnText(comp.RemainingText)).WithVolume(AdjustVolume(comp.iswhisper)).WithMaxDistance(AdjustDistance(comp.iswhisper));

            // Добавляем звук в очередь
            _audio.PlayLocal(comp.Sound, uid, null);

            comp.RemainingText = comp.RemainingText[1..];
            comp.NextBeepTime = now + TimeSpan.FromSeconds(AdjustWordSpacing(comp.RemainingText));

        }
    }

    private float AdjustWordSpacing(string text)
    {
        var words = text.Split(' ');
        if (words.Length > 5) // Большие предложения
        {
            return 0.1f;
        }
        return 0.03f; // Обычные слова
    }

    private float AdjustPitchBasedOnText(string text)
    {
        if (text.Contains("!") || text.Contains("?!"))
        {
            return 1.3f; // Увеличиваем тональность для восклицаний
        }
        if (text.Contains("?"))
        {
            return 0.9f; // Понижаем тональность для вопросов
        }
        return 1f; // Обычный тон
    }
    private float AdjustVolume(bool isWhisper)
    {
        var volume = -10f + SharedAudioSystem.GainToVolume(_volume/2);

        if (isWhisper)
            volume -= SharedAudioSystem.GainToVolume(5f);

        return volume;
    }

    private float AdjustDistance(bool isWhisper)
    {
        return isWhisper ? SharedChatSystem.WhisperMuffledRange : SharedChatSystem.VoiceRange;
    }
}
