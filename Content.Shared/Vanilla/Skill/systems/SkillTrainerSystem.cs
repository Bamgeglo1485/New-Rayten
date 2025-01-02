using Content.Shared.DoAfter;
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;

namespace Content.Shared.Vanilla.Skill;

    [Serializable, NetSerializable]
    public sealed partial class TrainEvent : SimpleDoAfterEvent
    {
        public skillType SkillType { get; set; }
        public int SkillIncreaseAmount { get; set; }
    }
