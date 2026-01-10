using Content.Shared.Chemistry;
namespace Content.Shared.Vanilla.Skill;

public abstract partial class SharedSkillSystem : EntitySystem
{
    private void OnChemScan(EntityUid uid, SkillComponent component, ref SolutionScanEvent args)
    {
        if (HasRequiredSkill(uid, SkillType.Medicine, SkillLevel.Basic, WithBeep: false))
            return;

        args.CanScan = false;
    }
}