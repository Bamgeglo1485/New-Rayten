using System;
using System.Threading;
using System.Threading.Tasks;
using Robust.Shared.Timing;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Server.Vanilla.Danger;
// using Robust.Shared.GameObjects;
// using Robust.Shared.Maths;

namespace Content.Server.Vanilla.NPC.HTN.PrimitiveTasks.Operators;

public sealed partial class VisitPatrolMarkerOperator : HTNOperator
{
    [Dependency] private IEntityManager _entManager = default!;
    [Dependency] private IGameTiming _timing = default!;

    [DataField("key", required: true)]
    public string Key = default!;

    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(
        NPCBlackboard blackboard, CancellationToken cancelToken)
    {
        if (!blackboard.TryGetValue<EntityUid>(Key, out var target, _entManager))
            return (false, null);

        return (true, null);
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        var target = blackboard.GetValue<EntityUid>(Key);

        if (!_entManager.TryGetComponent<PatrolMarkerComponent>(target, out var patrolmarker))
            return HTNOperatorStatus.Failed;

        patrolmarker.NewValidVisitAt = _timing.CurTime + patrolmarker.VisitTime;

        return HTNOperatorStatus.Finished;
    }

}