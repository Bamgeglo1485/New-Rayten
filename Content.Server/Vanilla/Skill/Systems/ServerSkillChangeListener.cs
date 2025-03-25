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
        if ( (component.CrimeLevel == SkillLevel.Advanced || component.CrimeLevel == SkillLevel.Expert) && !HasComp<AssComponent>(uid) )
        {
            AddComp<AssComponent>(uid);
        }
        if((component.CrimeLevel == SkillLevel.None || component.CrimeLevel == SkillLevel.Basic))
        {
            RemComp<AssComponent>(uid);
        }
    }
    
}