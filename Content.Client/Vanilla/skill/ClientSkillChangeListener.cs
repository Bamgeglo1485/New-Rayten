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
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly SkillInvisibleSystem _invis = default!;


    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SkillComponent, SkillLevelChangedEvent>(OnSkillLevelChanged);
        SubscribeLocalEvent<SkillComponent, LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<SkillComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(EntityUid uid, SkillComponent component, ComponentStartup args)
    {
        if (uid == _player.LocalEntity)
        {
            UpdateAllChem();
            UpdateAllInvisibleArchons();
        }

    }

    private void OnPlayerAttached(EntityUid uid, SkillComponent component, LocalPlayerAttachedEvent args)
    {
        UpdateAllChem();
        UpdateAllInvisibleArchons();
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
                UpdateAllChem();
                break;
            case skillType.Research:
                UpdateAllInvisibleArchons();
                break;
        }
    }

    private void UpdateAllChem()
    {
        var query = EntityQueryEnumerator<SolutionContainerVisualsComponent, AppearanceComponent>();
        while (query.MoveNext(out var uid, out var component, out var appearance))
            _appearance.QueueUpdate(uid, appearance);
    }
    private void UpdateAllInvisibleArchons()
    {
        var query = EntityQueryEnumerator<SkillInvisibleComponent>();
        while (query.MoveNext(out var ent, out var comp))
            _invis.UpdateVisibility(ent, comp);
    }
}

public readonly struct UpdateSkillUiEvent
{
    public UpdateSkillUiEvent()
    {
    }
}