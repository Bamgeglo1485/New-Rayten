using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Containers;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared.Vanilla.Entities.BrainWorm;

[RegisterComponent]
public sealed partial class BrainWormHostComponent : Component
{

    [DataField]
    public EntityUid HostedBrainWorm;

    [DataField]
    public bool MindUnderControl = false;

    [ViewVariables]
    public ContainerSlot BrainWormContainer = default!;

    [DataField]
    public EntityUid MindCage;

    public DoAfterId? ReControlDoAfter;

    /// <summary>
    ///     Размножение
    /// </summary>
    [DataField("actionBrainWormReproduce", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string ActionBrainWormReproduce = "ActionBrainWormReproduce";

    public EntityUid? ActionBrainWormReproduceEntity;

    /// <summary>
    ///     червь сам возвращает контроль
    /// </summary>
    [DataField("actionBrainWormReturnControl", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string ActionBrainWormReturnControl = "ActionBrainWormReturnControl";

    public EntityUid? ActionBrainWormReturnControlEntity;

}
public sealed partial class BrainWormReproduceEvent : InstantActionEvent
{
}
public sealed partial class BrainWormReturnControlActionEvent : InstantActionEvent
{
}
