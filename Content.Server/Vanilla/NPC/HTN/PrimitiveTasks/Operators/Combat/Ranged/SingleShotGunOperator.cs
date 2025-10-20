using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Shared.Mobs;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Map;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace Content.Server.Vanilla.NPC.HTN.PrimitiveTasks.Operators.Combat.Ranged
{
    public sealed partial class SingleShotGunOperator : HTNOperator
    {
        [Dependency] private readonly IEntityManager _entManager = default!;
        private SharedGunSystem _gunSystem = default!;

        [DataField("targetKey", required: true)]
        public string TargetKey = default!;

        public override void Initialize(IEntitySystemManager sysManager)
        {
            base.Initialize(sysManager);
            _gunSystem = sysManager.GetEntitySystem<SharedGunSystem>();
        }

        public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(
            NPCBlackboard blackboard, CancellationToken cancelToken)
        {
            if (!blackboard.TryGetValue<EntityUid>(TargetKey, out var target, _entManager))
                return (false, null);

            return (true, null);
        }

        public override void Startup(NPCBlackboard blackboard)
        {
            var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
            var target = blackboard.GetValue<EntityUid>(TargetKey);

            if (!_entManager.TryGetComponent<GunComponent>(owner, out var gun))
            {
                return;
            }

            _gunSystem.AttemptShoot(owner, owner, gun, new EntityCoordinates(target, Vector2.Zero), target);
        }

        public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
        {
            return HTNOperatorStatus.Finished;
        }

    }
}
