using Content.Shared.Objectives;
using Robust.Shared.Serialization;

namespace Content.Shared.Vanilla.Skill;

[Serializable, NetSerializable]
public sealed class UseSkillPointEvent(SkillType Skill) : EntityEventArgs
{
    public readonly SkillType skill = Skill;
}