using Content.Shared.Popups;
using Content.Shared.DoAfter;
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;

namespace Content.Shared.SkillTrainer;

public abstract class SharedSkillTrainerSystem : EntitySystem
{
    [Dependency] protected readonly SharedPopupSystem _popup = default!;
    [Dependency] protected readonly SharedDoAfterSystem _doAfter = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SkillTrainerComponent, TrainEvent>(HandleTrainEventShared);
    }

    protected virtual void HandleTrainEventShared(EntityUid uid, SkillTrainerComponent component, TrainEvent args)
    {
    }
}

    [Serializable, NetSerializable]
    public sealed partial class TrainEvent : SimpleDoAfterEvent
    {
        public string SkillType { get; set; } = string.Empty;
        public int SkillIncreaseAmount { get; set; }
        public int MaxLevel { get; set; }
    }
