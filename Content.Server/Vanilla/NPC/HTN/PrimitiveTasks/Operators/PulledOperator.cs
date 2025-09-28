using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Content.Shared.Pulling;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;
using PullingSystem = Content.Shared.Movement.Pulling.Systems.PullingSystem;

namespace Content.Server.Vanilla.NPC.HTN.PrimitiveTasks.Operators;

public sealed partial class PulledOperator : HTNOperator
{
    private PullingSystem _pulling = default!;

    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("key")]
    public string Key = string.Empty;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _pulling = sysManager.GetEntitySystem<PullingSystem>();
    }

    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(NPCBlackboard blackboard, CancellationToken cancelToken)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        var puller = _pulling.GetPuller(owner);

        if (puller != null)
        {
            return (true, new Dictionary<string, object>()
            {
                { Key, puller.Value }
            });
        }

        return (false, null);
    }
}
