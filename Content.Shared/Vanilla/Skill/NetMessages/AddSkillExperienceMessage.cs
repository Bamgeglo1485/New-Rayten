using Content.Shared.Objectives;
using Robust.Shared.Serialization;

namespace Content.Shared.Vanilla.Skill;

[Serializable, NetSerializable]
public sealed class RequestSkillAddEXPEvent : EntityEventArgs
{
    public readonly skillType skill;

    public RequestSkillAddEXPEvent(skillType Skill)
    {
        skill = Skill;
    }
}
