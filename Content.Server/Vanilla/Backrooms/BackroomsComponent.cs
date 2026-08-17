using Robust.Shared.Serialization;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Content.Shared.AlternateDimension;

namespace Content.Server.Backrooms;

[RegisterComponent]
public sealed partial class BackroomsComponent : Component
{
    [DataField]
    public float CopyChance = 0.05f;

    [DataField]
    public TimeSpan HumanCopyDelay { get; set; } = TimeSpan.FromSeconds(600);

    [DataField]
    public TimeSpan NextHumanCopy { get; set; } = TimeSpan.Zero;

    [DataField]
    public TimeSpan CleaningDelay { get; set; } = TimeSpan.FromSeconds(300);

    [DataField]
    public TimeSpan NextCleaning { get; set; } = TimeSpan.Zero;

    [DataField(customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string ClonePrototype = "DistortedCloneSpawner";

    [DataField]
    public EntityUid? RealGrid = null;

    [DataField]
    public ProtoId<AlternateDimensionPrototype> DimensionType = default!;
}
