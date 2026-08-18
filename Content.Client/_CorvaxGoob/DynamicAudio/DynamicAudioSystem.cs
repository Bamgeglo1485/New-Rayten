using Content.Shared._CorvaxGoob.DynamicAudio;
using Content.Shared._CorvaxGoob.DynamicAudio.Effects;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Audio;
using Robust.Client.Audio;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;

namespace Content.Client._CorvaxGoob.DynamicAudio;

public sealed partial class DynamicAudioSystem : EntitySystem
{
    [Dependency] private SharedDynamicAudioSystem _dynamicAudio = default!;
    [Dependency] private ISharedPlayerManager _playerManager = default!;
    [Dependency] private IAudioManager _audio = default!;
    [Dependency] private IConfigurationManager _cfg = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AudioComponent, ComponentAdd>(OnAudioAdd);
        SubscribeLocalEvent<DynamicAudioComponent, ComponentStartup>(OnEffectedAudioStartup, after: [typeof(SharedAudioSystem)]);
        SubscribeLocalEvent<InBarotraumaAudioEffectComponent, ComponentShutdown>(OnBarotraumaShutdown);
        SubscribeLocalEvent<InBarotraumaAudioEffectComponent, ComponentStartup>(OnBarotraumaStartup);
        SubscribeLocalEvent<InBarotraumaAudioEffectComponent, LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<InBarotraumaAudioEffectComponent, LocalPlayerDetachedEvent>(OnPlayerDetached);
    }

    private float _originalVolume;

    private void OnBarotraumaStartup(Entity<InBarotraumaAudioEffectComponent> ent, ref ComponentStartup args)
    {
        if (_playerManager.LocalEntity == ent.Owner)
        {
            _originalVolume = _cfg.GetCVar(CCVars.AudioMasterVolume);
            _audio.SetMasterGain(0);
        }
    }

    private void OnBarotraumaShutdown(Entity<InBarotraumaAudioEffectComponent> ent, ref ComponentShutdown args)
    {
        if (_playerManager.LocalEntity == ent.Owner)
        {
            _audio.SetMasterGain(_originalVolume);
        }
    }

    private void OnPlayerDetached(Entity<InBarotraumaAudioEffectComponent> ent, ref LocalPlayerDetachedEvent args)
    {
        if (_playerManager.LocalEntity == ent.Owner)
        {
            _audio.SetMasterGain(_originalVolume);
        }
    }

    private void OnPlayerAttached(Entity<InBarotraumaAudioEffectComponent> ent, ref LocalPlayerAttachedEvent args)
    {
        if (_playerManager.LocalEntity == ent.Owner)
        {
            _originalVolume = _cfg.GetCVar(CCVars.AudioMasterVolume);
            _audio.SetMasterGain(0);
        }
    }

    private void OnAudioAdd(Entity<AudioComponent> ent, ref ComponentAdd args)
    {
        if (!_playerManager.LocalEntity.HasValue
            || !TryComp<EyeComponent>(_playerManager.LocalEntity.Value, out var eye)
            || !eye.DrawFov)
            return;

        EnsureComp<DynamicAudioComponent>(ent);
    }

    private void OnEffectedAudioStartup(Entity<DynamicAudioComponent> ent, ref ComponentStartup args)
    {
        if (!TryComp<AudioComponent>(ent.Owner, out var audio) ||
            TerminatingOrDeleted(ent)
            || Paused(ent)
            || audio.Global)
            return;

        _dynamicAudio.ApplyAudioEffect((ent, audio));
    }
}
