using Robust.Shared.Timing;
using Content.Shared.Audio; 
using Content.Shared.Chat;
using Robust.Shared.Random;
using Robust.Shared.Audio;
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
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IEyeManager _eyeManager = default!;
    [Dependency] private readonly ExamineSystem? _examine = default;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly TransformSystem? _transform = default;
    public float _volume = 0.0f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<UndertaleSpeechComponent, UndertaleSpeechBeepEvent>(onBeep);
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

    private void onBeep(EntityUid uid, UndertaleSpeechComponent comp, UndertaleSpeechBeepEvent args)
    {
        var player = _player.LocalEntity;

        if (comp.Sound == null || _examine == null)
        {
            RemCompDeferred<UndertaleSpeechComponent>(uid);
            return;
        }

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
            return;
        }

        var sound = comp.Sound;

        sound.Params = AudioParams.Default.WithVolume(AdjustVolume(comp.iswhisper)).WithMaxDistance(AdjustDistance(comp.iswhisper));

        _audio.PlayLocal(comp.Sound, uid, null);
    }
    public float AdjustVolume(bool isWhisper)
    {
        var volume = -10f + SharedAudioSystem.GainToVolume(_volume/1.5f);

        if (isWhisper)
            volume -= SharedAudioSystem.GainToVolume(5f);

        return volume;
    }

    private float AdjustDistance(bool isWhisper)
    {
        return isWhisper ? SharedChatSystem.WhisperMuffledRange : SharedChatSystem.VoiceRange;
    }
}
public sealed class UndertaleSpeechBeepEvent : EntityEventArgs
{
    public char Character { get; }

    public UndertaleSpeechBeepEvent(char character)
    {
        Character = character;
    }
}
