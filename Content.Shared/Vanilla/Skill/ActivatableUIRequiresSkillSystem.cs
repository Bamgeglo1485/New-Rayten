using Content.Shared.UserInterface;

namespace Content.Shared.Vanilla.Skill;

public abstract class SharedActivatableUIRequiresSkillSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ActivatableUIRequiresSkillComponent, ActivatableUIOpenAttemptEvent>(OnActivate);
    }

    protected abstract void OnActivate(EntityUid uid, ActivatableUIRequiresSkillComponent component, ref ActivatableUIOpenAttemptEvent args);
}
