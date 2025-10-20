using System.Threading;
using System.Threading.Tasks;
using Content.Shared.Hands.EntitySystems;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Components;
using Content.Server.NPC;
using Content.Shared.DoAfter;
using Content.Shared.Hands.Components;
using Content.Shared.Cuffs;
using Content.Shared.Cuffs.Components;
using Content.Shared.Vanilla.Dominator;

namespace Content.Server.Vanilla.NPC.HTN.PrimitiveTasks.Operators.Securitron;

public sealed partial class CuffOperator : HTNOperator, IHtnConditionalShutdown
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    private SharedCuffableSystem _handcuff = default!;
    private SharedHandsSystem _hands = default!;

    [DataField("shutdownState")]
    public HTNPlanState ShutdownState { get; private set; } = HTNPlanState.TaskFinished;

    [DataField("targetKey", required: true)]
    public string TargetKey = default!;

    [DataField("cuffKey", required: true)]
    public string CuffKey = "Handcuff";
    private bool _started = false;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _handcuff = sysManager.GetEntitySystem<SharedCuffableSystem>();
        _hands = sysManager.GetEntitySystem<SharedHandsSystem>();
    }

    public override void Startup(NPCBlackboard blackboard)
    {
        base.Startup(blackboard);
        _started = false;

        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!_entManager.TryGetComponent<SecurityMarkerComponent>(owner, out var security))
            return;

        if (security.HandCuffContainer.ContainedEntity is not { } cuffs)
            return; // наручников нет в контейнере

        // Пробуем взять в руку
        if (_entManager.TryGetComponent<HandsComponent>(owner, out var hands))
        {
            if (_hands.TryPickupAnyHand(owner, cuffs))
            {
                // Записываем в blackboard активные наручники
                blackboard.SetValue(CuffKey, cuffs);
            }
        }
    }

    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(
        NPCBlackboard blackboard, CancellationToken cancelToken)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        if (!blackboard.TryGetValue<EntityUid>(TargetKey, out var target, _entManager))
            return (false, null);

        // целевой должен быть cuffable
        if (!_entManager.HasComponent<CuffableComponent>(target))
            return (false, null);

        // Мы должны быть секьюритроном
        if (!_entManager.TryGetComponent<SecurityMarkerComponent>(owner, out var security))
            return (false, null);

        // наручников нет в контейнере
        if (security.HandCuffContainer.ContainedEntity is not { } cuffs)
            return (false, null);

        return (true, null);
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        if (!blackboard.TryGetValue<EntityUid>(TargetKey, out var target, _entManager) ||
            !blackboard.TryGetValue<EntityUid>(CuffKey, out var handcuff, _entManager))
            return HTNOperatorStatus.Failed;

        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!_started)
        {
            if (!_handcuff.TryCuffing(owner, target, handcuff))
                return HTNOperatorStatus.Failed;

            _started = true;

            return HTNOperatorStatus.Continuing;
        }

        if (!_entManager.TryGetComponent<CuffableComponent>(target, out var cuffable))
            return HTNOperatorStatus.Failed;

        if (cuffable.CuffedHandCount > 0)
            return HTNOperatorStatus.Finished;

        if (!_entManager.HasComponent<ActiveDoAfterComponent>(owner))
            return HTNOperatorStatus.Failed;

        return HTNOperatorStatus.Continuing;
    }

    public void ConditionalShutdown(NPCBlackboard blackboard)
    {
        _started = false;
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!_entManager.TryGetComponent<SecurityMarkerComponent>(owner, out var security))
            return;

        if (_hands.GetActiveItem(owner) is { } heldEntity)
            _hands.TryDropIntoContainer(owner, heldEntity, security.HandCuffContainer);

    }

    public override void TaskShutdown(NPCBlackboard blackboard, HTNOperatorStatus status)
    {
        base.TaskShutdown(blackboard, status);
        ConditionalShutdown(blackboard);
    }

}
