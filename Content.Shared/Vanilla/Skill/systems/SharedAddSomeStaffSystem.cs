namespace Content.Shared.Vanilla.Skill;

public sealed class SharedAddSomeStaffSystem : EntitySystem
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
                if ( (component.CrimeLevel == SkillLevel.Advanced || component.CrimeLevel == SkillLevel.Expert) && !HasComp<AssComponent>(uid) )
                {
                    AddComp<AssComponent>(uid);
                }
                if((component.CrimeLevel == SkillLevel.None || component.CrimeLevel == SkillLevel.Basic))
                {
                    RemComp<AssComponent>(uid);
                }
                
                break;
        }
    }
    
}