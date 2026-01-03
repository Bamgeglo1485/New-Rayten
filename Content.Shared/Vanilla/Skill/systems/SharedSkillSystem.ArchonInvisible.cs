using Content.Shared.Hands;
namespace Content.Shared.Vanilla.Skill;

public abstract partial class SharedSkillSystem : EntitySystem
{
    private void OnEquippedHand(EntityUid uid, SkillInvisibleComponent comp, ref GotEquippedHandEvent args)
    {
        comp.Visible = true;
        Dirty(uid, comp);
    }
}
