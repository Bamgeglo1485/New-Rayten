using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Shared.Vanilla.Dominator;
using Content.Shared.Cuffs.Components;
using Content.Shared.Hands.EntitySystems;
using System.Threading;
using System.Threading.Tasks;

namespace Content.Server.Vanilla.NPC.HTN.PrimitiveTasks.Operators.Securitron;

public sealed partial class InsertCuffsOperator : HTNOperator
{
    [Dependency] private IEntityManager _entManager = default!;

    private SharedHandsSystem _hands = default!;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _hands = sysManager.GetEntitySystem<SharedHandsSystem>();
    }

    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(
        NPCBlackboard blackboard, CancellationToken cancelToken)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        // Мы должны быть секьюритроном
        if (!_entManager.TryGetComponent<SecuritronComponent>(owner, out var security))
            return (false, null);

        // наручники уже есть
        if (security.HandCuffContainer.ContainedEntity != null)
            return (false, null);

        if (_hands.GetActiveItem(owner) is { } heldEntity)
        {
            if (_entManager.HasComponent<HandcuffComponent>(heldEntity))
                return (false, null);
        }

        return (true, null);
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!_entManager.TryGetComponent<SecuritronComponent>(owner, out var security))
            return HTNOperatorStatus.Failed;

        if (_hands.GetActiveItem(owner) is { } heldEntity)
        {
            if (_hands.TryDropIntoContainer(owner, heldEntity, security.HandCuffContainer))
                return HTNOperatorStatus.Finished;
        }

        return HTNOperatorStatus.Failed;
    }
}
