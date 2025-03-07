using Content.Shared.Objectives;
using Robust.Shared.Serialization;

namespace Content.Shared.Vanilla.Skill;

[Serializable, NetSerializable]
public sealed class UseSkillPointEvent : EntityEventArgs
{
    public readonly skillType skill;

    public UseSkillPointEvent(skillType Skill)
    {
        skill = Skill;
    }
}