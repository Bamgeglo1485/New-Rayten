using Content.Shared.Objectives;
using Robust.Shared.Serialization;

namespace Content.Shared.Vanilla.Skill;

[Serializable, NetSerializable]
public sealed class RequestSkillAddEXPEvent : EntityEventArgs
{
    public readonly string skill;

    // Исправляем параметр конструктора
    public RequestSkillAddEXPEvent(string Skill)
    {
        skill = Skill;  // Обработка null
    }
}
