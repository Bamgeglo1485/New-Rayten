using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared.Humanoid.Prototypes;
using Content.Corvax.Interfaces.Shared;
using Content.Shared.Random;
using Content.Shared.Vanilla.Sponsor;
using Robust.Shared.Collections;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;
using Content.Shared.Preferences;
using Content.Shared.Vanilla.Skill;

namespace Content.Shared.Vanilla.RoleSkills;


[Serializable, NetSerializable, DataDefinition]
public sealed partial class RoleSkills : IEquatable<RoleSkills>
{

    [DataField]
    public ProtoId<RoleSkillsPrototype> Role;

    //Навыки, выбранные игроком
    [DataField]
    public Dictionary<skillType, SkillLevel> AddedBasicSkills = new();
    [DataField]
    public HashSet<skillType> AddedEasySkills = new();

    [DataField]
    public bool IsValid = false;

    public RoleSkills(ProtoId<RoleSkillsPrototype> role)
    {
        Role = role;
    }

    public RoleSkills Clone()
    {
        return new RoleSkills(Role)
        {
            AddedBasicSkills = new Dictionary<skillType, SkillLevel>(AddedBasicSkills),
            AddedEasySkills = new HashSet<skillType>(AddedEasySkills),
            IsValid = IsValid,
        };
    }


    public void EnsureValid(HumanoidCharacterProfile profile, ICommonSession session, IDependencyCollection collection)
    {
        var protoManager = collection.Resolve<IPrototypeManager>();
        var skillpoints = SharedRoleSkillsSystem.skillpoints;

        List<(skillType Skill, SkillLevel Level, int Experience)> generalbasicskills = new List<(skillType Skill, SkillLevel Level, int Experience)>
        {
            (skillType.RangeWeapon, SkillLevel.None, 0),
            (skillType.MeleeWeapon, SkillLevel.None, 0),
            (skillType.Medicine, SkillLevel.None, 0),
            (skillType.Chemistry, SkillLevel.None, 0),
            (skillType.Engineering, SkillLevel.None, 0),
            (skillType.Building, SkillLevel.None, 0),
            (skillType.Research, SkillLevel.None, 0),
            (skillType.Crime, SkillLevel.None, 0)
        };

        List<(skillType Skill, bool have, int Experience)> generaleasyskills = new List<(skillType Skill, bool have, int Experience)>
        {
            (skillType.Piloting, false, 0),
            (skillType.Botany, false, 0),
            (skillType.MusInstruments, false, 0),
            (skillType.Bureaucracy, false, 0),
            (skillType.Atmosphere, false, 0)
        };

        // 1. Проверка, что прототип навыксета существует
        if (!protoManager.TryIndex(Role, out RoleSkillsPrototype? roleProto))
        {
            SetDefault(profile);
            return;
        }

        // 2. Проверка на навыки
        void ApplyBasicSkills(Dictionary<skillType, SkillLevel>? Skills)
        {
            if (Skills == null)
                return;

            foreach (var (skill, level) in Skills)
            {
                int index = generalbasicskills.FindIndex(s => s.Skill == skill);
                if (index == -1)
                    continue;

                var current = generalbasicskills[index];
                int currentLevel = (int)current.Level;
                int addedLevel = (int)level;
                int total = currentLevel + addedLevel;

                SkillLevel newLevel = total > (int)SkillLevel.Expert
                    ? SkillLevel.Expert
                    : (SkillLevel)total;

                int finalLevel = (int)newLevel;

                if (total > (int)SkillLevel.Expert)
                    skillpoints += total - (int)SkillLevel.Expert;

                int delta = finalLevel - currentLevel;
                skillpoints -= delta;

                generalbasicskills[index] = (skill, newLevel, 0);
            }
        }
        void ApplyEasySkills(HashSet<skillType>? Skills)
        {
            if (Skills == null)
                return;

            foreach (var skill in Skills)
            {
                int index = generaleasyskills.FindIndex(s => s.Skill == skill);
                if (index == -1)
                    continue;

                var current = generaleasyskills[index];
                if (current.have)
                {
                    skillpoints += 1;
                }
                else
                {
                    skillpoints -= 1;
                    generaleasyskills[index] = (skill, true, 0);
                }
            }
        }
        //считаем выбор игрока
        ApplyBasicSkills(AddedBasicSkills);
        ApplyEasySkills(AddedEasySkills);
        //считаем навыки роли
        ApplyBasicSkills(roleProto.BasicSkills);
        ApplyEasySkills(roleProto.EasySkills);

        if (skillpoints != 0)
        {
            SetDefault(profile);
            return;
        }
        IsValid = true;
    }

    public void SetDefault(HumanoidCharacterProfile? profile)
    {
        if (profile == null)
            return;
        AddedBasicSkills = new();
        AddedEasySkills = new();
        IsValid = false;
    }

    public bool Equals(RoleSkills? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;

        return Role.Equals(other.Role)
            && AddedBasicSkills.Count == other.AddedBasicSkills.Count
            && AddedBasicSkills.All(kv => other.AddedBasicSkills.TryGetValue(kv.Key, out var val) && val == kv.Value)
            && AddedEasySkills.SetEquals(other.AddedEasySkills);
    }


    public override bool Equals(object? obj)
    {
        return obj is RoleSkills other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Role, AddedBasicSkills, AddedEasySkills);
    }
}
