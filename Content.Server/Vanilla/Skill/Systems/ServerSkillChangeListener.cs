using Content.Shared.Vanilla.Skill;
namespace Content.Server.Vanilla.Skill;

public sealed class ServerSkillChangeListener : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SkillComponent, SkillLevelChangedEvent>(OnSkillLevelChanged);
    }

    private void OnSkillLevelChanged(EntityUid uid, SkillComponent component, SkillLevelChangedEvent args)
    {
        switch (args.Skill)
        {
            case skillType.Crime:
                ReactOnCrimeLevelChanged(uid, component);
                break;
        }
    }
    private void ReactOnCrimeLevelChanged(EntityUid uid, SkillComponent component)
    {
        if (component.CrimeLevel == SkillLevel.None)
        {
            RemComp<AssComponent>(uid);
            RemComp<ThievingComponent>(uid);
        }
        if (component.CrimeLevel == SkillLevel.Basic)
        {
            RemComp<AssComponent>(uid);
            RemComp<ThievingComponent>(uid);
        }
        if (component.CrimeLevel == SkillLevel.Advanced)
        {
            AddComp<AssComponent>(uid);
            RemComp<ThievingComponent>(uid);
        }
        if (component.CrimeLevel == SkillLevel.Expert)
        {
            AddComp<ThievingComponent>(uid);
        }
    }

}
