using Content.Shared.Objectives;
using Robust.Shared.Serialization;
using Content.Shared.DoAfter;
namespace Content.Shared.Vanilla.Skill;

[Serializable, NetSerializable]
    public sealed partial class SkillBookEvent : SimpleDoAfterEvent
    {
        public skillType SkillType { get; set; }
        public int SkillIncreaseAmount { get; set; }
    }