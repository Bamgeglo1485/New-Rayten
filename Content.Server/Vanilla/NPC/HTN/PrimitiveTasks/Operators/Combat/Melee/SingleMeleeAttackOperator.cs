using System;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Shared.Mobs;
using Content.Shared.Weapons.Melee;
using Content.Shared.CombatMode;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;

namespace Content.Server.Vanilla.NPC.HTN.PrimitiveTasks.Operators.Combat.Melee
{
    public sealed partial class SingleMeleeAttackOperator : HTNOperator
    {
        [Dependency] private IEntityManager _entManager = default!;
        private SharedMeleeWeaponSystem _meleeSystem = default!;
        private SharedCombatModeSystem _combat = default!;


        [DataField("targetKey", required: true)]
        public string TargetKey = default!;

        public override void Initialize(IEntitySystemManager sysManager)
        {
            base.Initialize(sysManager);
            _meleeSystem = sysManager.GetEntitySystem<SharedMeleeWeaponSystem>();
            _combat = _entManager.System<SharedCombatModeSystem>();
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

            if (!_meleeSystem.TryGetWeapon(owner, out var weaponUid, out var weapon))
                return;

            _combat.SetInCombatMode(owner, true);
            _meleeSystem.AttemptLightAttack(owner, weaponUid, weapon, target);
            _combat.SetInCombatMode(owner, false);
        }



        public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
        {
            // Задача выполнена сразу после одного удара
            return HTNOperatorStatus.Finished;
        }
    }
}
