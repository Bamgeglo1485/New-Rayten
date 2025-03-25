using Content.Shared.Vanilla.Skill;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Client.Player;
using Robust.Shared.Player;

namespace Content.Client.Vanilla.Skill;

public sealed class ClientSkillChangeListener : EntitySystem
{ 
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SkillComponent, SkillLevelChangedEvent>(OnSkillLevelChanged);
    }

    private void OnSkillLevelChanged(EntityUid uid, SkillComponent component, SkillLevelChangedEvent args)
    {
        if (uid == _playerManager.LocalPlayer?.ControlledEntity)
        {
            if(!args.IsExp)
                _audio.PlayGlobal("/Audio/Vanilla/SkillSystem/levelup.ogg", Filter.Local(), false, audioParams: AudioParams.Default.WithVolume(-6f));
                
            RaiseLocalEvent(new UpdateSkillUiEvent());
        }
    }
    
}
public readonly struct UpdateSkillUiEvent
{
    public UpdateSkillUiEvent()
    {
    }
}