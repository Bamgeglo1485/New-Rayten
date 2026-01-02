using Content.Shared.Chemistry;
namespace Content.Shared.Vanilla.Skill;

public sealed partial class SharedSkillSystem : EntitySystem
{
    private void OnChemScan(EntityUid uid, SkillComponent component, ref SolutionScanEvent args)
    {
        if (HasRequiredSkill(uid, SkillType.Medicine, SkillLevel.Basic))
            return;

        args.CanScan = false;
    }
}