using Content.Shared.Chat;
using Content.Shared.Corvax.CCCVars;
using Content.Shared.Vanilla.VoiceSpeech;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;

namespace Content.Client.Vanilla.VoiceSpeech;

public sealed partial class VoiceSpeechSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    public float Volume = 0.0f;

    public override void Initialize()
    {
        base.Initialize();
        _cfg.OnValueChanged(CCCVars.TTSVolume, OnTtsVolumeChanged, true);
    }

    public void Beep(EntityUid uid, VoiceEmitterComponent comp)
    {
        if (comp.VoicePrototypeId == null)
            return;

        _audio.PlayLocal(comp.Voice, uid, null);
    }

    public AudioParams SetVolume(bool whisper, VoiceEmitterComponent comp, float baseVolume)
    {
        return AudioParams.Default
                .WithPitchScale(comp.Pitch)
                .WithVariation(0.05f)
                .WithVolume(AdjustVolume(whisper, baseVolume))
                .WithMaxDistance(whisper ? SharedChatSystem.WhisperMuffledRange : SharedChatSystem.VoiceRange);
    }

    public float AdjustVolume(bool isWhisper, float baseVolume)
    {
        if (Volume == 0)
            baseVolume = 0;

        var volume = -10f + SharedAudioSystem.GainToVolume(Volume + baseVolume);

        if (isWhisper)
            volume -= SharedAudioSystem.GainToVolume(5f);

        return volume;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _cfg.UnsubValueChanged(CCCVars.TTSVolume, OnTtsVolumeChanged);
    }

    private void OnTtsVolumeChanged(float volume)
    {
        Volume = volume;
    }
}
