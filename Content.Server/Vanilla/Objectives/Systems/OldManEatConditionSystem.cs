using Content.Server.Vanilla.Objectives.Components;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Objectives.Components;

namespace Content.Server.Vanilla.Objectives.Systems;

public sealed class OldManEatConditionSystem : EntitySystem
{
    [Dependency] private readonly SharedMindSystem _mind = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<OldManEatConditionComponent, ObjectiveGetProgressEvent>(OnGetProgress);
    }

    private void OnGetProgress(EntityUid uid, OldManEatConditionComponent comp, ref ObjectiveGetProgressEvent args)
    {
        args.Progress = comp.Completed ? 1f : 0f;
    }
    public void SetCompleted(Entity<MindContainerComponent?> mob, bool completed = true)
    {

        if (_mind.GetMind(mob, mob.Comp) is not { } mindId)
            return;

        if (!_mind.TryFindObjective(mindId, "OldManEatObjective", out var obj))
            return;

        if (TryComp<OldManEatConditionComponent>(obj, out var oldmaneat))
            oldmaneat.Completed = completed;
    }
}
