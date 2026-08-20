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
