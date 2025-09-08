namespace Content.Shared.Vanilla.RoleSkills;

public sealed class SharedRoleSkillsSystem : EntitySystem
{
    public const int skillpoints = 14;
    public static string GetJobPrototype(string? roleSkills)
    {
        if (string.IsNullOrEmpty(roleSkills))
            return string.Empty;
        return "Job" + roleSkills;
    }

}
