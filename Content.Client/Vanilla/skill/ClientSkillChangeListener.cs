using Content.Shared.Vanilla.Skill;
using Content.Shared.Chemistry.Components;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Client.Player;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Client.GameObjects;

namespace Content.Client.Vanilla.Skill;

public sealed class ClientSkillChangeListener : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SkillComponent, SkillLevelChangedEvent>(OnSkillLevelChanged);
        SubscribeLocalEvent<SkillComponent, LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<SkillComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(EntityUid uid, SkillComponent component, MapInitEvent args)
    {
        if (uid == _player.LocalEntity)
        {
            UpdateAllChem(component);
        }
    }

    private void OnPlayerAttached(EntityUid uid, SkillComponent component, LocalPlayerAttachedEvent args)
    {
        UpdateAllChem(component);
    }

    private void OnSkillLevelChanged(EntityUid uid, SkillComponent component, SkillLevelChangedEvent args)
    {
        if (uid == _player.LocalEntity)
        {
            if (!args.IsExp)
                _audio.PlayGlobal("/Audio/Vanilla/SkillSystem/levelup.ogg", Filter.Local(), false, audioParams: AudioParams.Default.WithVolume(-6f));

            RaiseLocalEvent(new UpdateSkillUiEvent());
        }
        switch (args.Skill)
        {
            case skillType.Medicine:
                UpdateAllChem(component);
                break;
        }
    }

    private void UpdateAllChem(SkillComponent skillComp)
    {
        var query = EntityQueryEnumerator<SolutionContainerVisualsComponent, AppearanceComponent>();
        while (query.MoveNext(out var uid, out var component, out var appearance))
        {
            _appearance.QueueUpdate(uid, appearance);
        }
    }
}

public readonly struct UpdateSkillUiEvent
{
    public UpdateSkillUiEvent()
    {
    }
}