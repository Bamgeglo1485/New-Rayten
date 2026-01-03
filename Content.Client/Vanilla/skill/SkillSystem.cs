using Content.Shared.Vanilla.Skill;
using Content.Shared.Chemistry.Components;
using Robust.Client.Player;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Client.GameObjects;

namespace Content.Client.Vanilla.Skill;

public sealed partial class SkillSystem : SharedSkillSystem
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SkillComponent, AfterAutoHandleStateEvent>(OnHandleState);
        SubscribeLocalEvent<SkillComponent, LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<SkillComponent, ComponentStartup>(OnStartup);

        SubscribeLocalEvent<SkillInvisibleComponent, AfterAutoHandleStateEvent>(OnHandleState);
        SubscribeLocalEvent<SkillInvisibleComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<SkillInvisibleComponent, ComponentStartup>(OnStartup);
    }

    private void OnHandleState(EntityUid uid, SkillComponent component, AfterAutoHandleStateEvent args)
    {
        if (uid != _player.LocalEntity)
            return;

        RaiseLocalEvent(new UpdateSkillUiEvent());
    }

    public override void UpdateAllSystems(EntityUid uid, SkillComponent component)
    {
        UpdateAllChem();
        UpdateAllInvisibleArchons();
        UpdateGun(uid, component);
    }

    private void OnStartup(EntityUid uid, SkillComponent component, ComponentStartup args)
    {
        if (uid == _player.LocalEntity)
        {
            UpdateAllSystems(uid, component);
        }

    }
    private void OnPlayerAttached(EntityUid uid, SkillComponent component, LocalPlayerAttachedEvent args)
    {
        UpdateAllSystems(uid, component);
    }

    private void UpdateAllChem()
    {
        var query = EntityQueryEnumerator<SolutionContainerVisualsComponent, AppearanceComponent>();
        while (query.MoveNext(out var uid, out var component, out var appearance))
            _appearance.QueueUpdate(uid, appearance);
    }
}

public readonly struct UpdateSkillUiEvent
{
    public UpdateSkillUiEvent()
    {
    }
}