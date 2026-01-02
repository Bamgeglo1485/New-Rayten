using Content.Shared.Hands;
namespace Content.Shared.Vanilla.Skill;

public abstract class SharedSkillInvisibleSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SkillInvisibleComponent, GotEquippedHandEvent>(OnEquippedHand);
    }
    private void OnEquippedHand(EntityUid uid, SkillInvisibleComponent comp, ref GotEquippedHandEvent args)
    {
        comp.Visible = true;
        Dirty(uid, comp);
    }
}
