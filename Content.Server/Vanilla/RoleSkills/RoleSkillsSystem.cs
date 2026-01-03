using Content.Server.Roles;
using Content.Server.Ghost.Roles;
using Content.Server.Administration.Systems;
using Content.Shared.Administration;
using Content.Server.Vanilla.Skill;
using Content.Server.GameTicking.Events;
using Content.Server.Preferences.Managers;
using Content.Shared.Vanilla.Background;
using Content.Shared.Vanilla.TDM;
using Content.Shared.Vanilla.Skill;
using Content.Shared.Vanilla.RoleSkills;
using Content.Shared.Roles;
using Content.Shared.Preferences;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Server.Vanilla.RoleSkillsSystem;

public sealed class RoleSkillsSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<IsRoleAllowedEvent>(OnIsRoleAllowed);
    }
    private void OnIsRoleAllowed(ref IsRoleAllowedEvent ev)
    {
        var prefManager = IoCManager.Resolve<IServerPreferencesManager>();
        var prefs = prefManager.GetPreferences(ev.Player.UserId);

        var profile = prefs.SelectedCharacter as HumanoidCharacterProfile;
        if (profile == null)
            return;

        // Если в ивенте нет конкретных профессий, ничего не проверяем
        if (ev.Jobs == null || ev.Jobs.Count == 0)
            return;

        foreach (var jobId in ev.Jobs)
        {
            if (!_prototype.TryIndex<JobPrototype>(jobId, out var jobProto))
                continue;

            var jobProtoId = SharedRoleSkillsSystem.GetJobPrototype(jobProto.ID);

            if (!_prototype.TryIndex<RoleSkillsPrototype>(jobProtoId, out var roleSkillsProto))
                continue;

            if (!profile.RoleSkills.TryGetValue(jobProtoId, out var roleSkills))
            {
                ev.Cancelled = true;
                return;
            }

            if (!roleSkills.IsValid)
            {
                ev.Cancelled = true;
                return;
            }
        }
    }


    public void ApplyRoleSkills(EntityUid uid, RoleSkills? roleSkills)
    {
        if (roleSkills == null)
            return;

        //итоговые навыки
        Dictionary<SkillType, SkillLevel> generalbasicskills = new()
        {
            { SkillType.Weapon, SkillLevel.None },
            { SkillType.Medicine, SkillLevel.None },
            { SkillType.Engineering, SkillLevel.None }
        };
        HashSet<SkillType> generaleasyskills = [];

        //обнуляем навык
        RemComp<SkillComponent>(uid);
        var skillComp = EnsureComp<SkillComponent>(uid);

        //Складываем навыки и особенности с предысторий
        if (_prototype.TryIndex(roleSkills.Role, out var roleskills))
        {
            layDownBacic(roleskills.BasicSkills);
            layDownEasy(roleskills.EasySkills);
        }

        layDownBacic(roleSkills.AddedBasicSkills);
        layDownEasy(roleSkills.AddedEasySkills);

        //Передаём навыки и особенности в сущность
        skillComp.BasicSkills = generalbasicskills;
        skillComp.EasySkills = generaleasyskills;

        void layDownBacic(Dictionary<SkillType, SkillLevel>? backgroundSkills)
        {
            if (backgroundSkills == null)
                return;

            foreach (var (skill, level) in backgroundSkills)
            {
                if (!generalbasicskills.TryGetValue(skill, out var currentLevel))
                    continue;

                int total = (int)currentLevel + (int)level;
                SkillLevel newLevel = total > (int)SkillLevel.Expert
                    ? SkillLevel.Expert
                    : (SkillLevel)total;

                generalbasicskills[skill] = newLevel;
            }
        }
        void layDownEasy(HashSet<SkillType>? backgroundSkills)
        {
            if (backgroundSkills == null)
                return;

            foreach (var skill in backgroundSkills)
            {
                generaleasyskills.Add(skill);
            }
        }
    }
}
