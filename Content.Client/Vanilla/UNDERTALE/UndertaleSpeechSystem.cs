using Robust.Shared.Timing;
using Content.Shared.Audio; 
using Content.Shared.Chat;
using Robust.Shared.Random;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using System.Linq;
using Content.Shared.Corvax.CCCVars;
using Content.Shared.Vanilla.UndertaleSpeech;
using Content.Client.Examine;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Client.Player;
using Robust.Client.GameObjects;
using Robust.Shared.Map;

namespace Content.Client.Vanilla.UndertaleSpeech;

public sealed class UndertaleSpeechSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    public float _volume = 0.0f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<UndertaleSpeechEmitterComponent, UndertaleSpeechBeepEvent>(onBeep);
        SubscribeLocalEvent<UndertaleSpeechEmitterComponent, MapInitEvent>(onMapInit);
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
    private void onMapInit(EntityUid uid, UndertaleSpeechEmitterComponent comp, MapInitEvent args)
    {
        if (comp.VoicePrototypeId == null || !_prototypeManager.TryIndex<UndertaleSpeechPrototype>(comp.VoicePrototypeId, out var proto))
            return;

        comp.Voice = proto.Voice;
    }

    private void onBeep(EntityUid uid, UndertaleSpeechEmitterComponent comp, UndertaleSpeechBeepEvent args)
    {
        if (comp.VoicePrototypeId == null )
            return;

        var sound = comp.Voice;

        sound.Params = AudioParams.Default.WithPitchScale(comp.Pitch).WithVolume(AdjustVolume(comp.iswhisper)).WithMaxDistance(AdjustDistance(comp.iswhisper));

        _audio.PlayLocal(sound, uid, null);
    }

    public float AdjustVolume(bool isWhisper)
    {
        var volume = -10f + SharedAudioSystem.GainToVolume(_volume);

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
