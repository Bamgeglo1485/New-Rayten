
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
        SubscribeLocalEvent<VoiceEmitterComponent, VoiceSpeechBeepEvent>(onBeep);
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

    private void onBeep(EntityUid uid, VoiceEmitterComponent comp, VoiceSpeechBeepEvent args)
    {
        if (comp.VoicePrototypeId == null )
            return;
        _audio.PlayLocal(comp.Voice, uid, null);
    }
}
public sealed class VoiceSpeechBeepEvent : EntityEventArgs
{
    public char Character { get; }

    public VoiceSpeechBeepEvent(char character)
    {
        Character = character;
    }
}
