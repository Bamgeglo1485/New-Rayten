
using Content.Shared.Audio;
using Content.Shared.Chat;
using Content.Shared.Corvax.CCCVars;
using Content.Shared.Vanilla.VoiceSpeech;
using Robust.Shared.Random;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Timing;
using Robust.Shared.Prototypes;
using Robust.Client.Player;
using Robust.Client.GameObjects;
using Content.Client.Examine;
using Robust.Client.Graphics;
using System.Linq;

namespace Content.Client.Vanilla.VoiceSpeech;

public sealed class VoiceSpeechSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    public float _volume = 0.0f;

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

    public AudioParams SetVolume(bool whisper, VoiceEmitterComponent comp)
    {
        return AudioParams.Default
                .WithPitchScale(comp.Pitch)
                .WithVariation(0.05f)
                .WithVolume(AdjustVolume(whisper))
                .WithMaxDistance(whisper ? SharedChatSystem.WhisperMuffledRange : SharedChatSystem.VoiceRange);
    }

    private float AdjustVolume(bool isWhisper)
    {
        float volume = -10f + SharedAudioSystem.GainToVolume(_volume);

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
        _volume = volume;
    }
}
