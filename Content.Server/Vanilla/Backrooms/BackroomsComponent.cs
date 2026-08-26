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
    public float LineCopyChance = 0.1f;

    [DataField]
    public int MaxLine = 6;

    [DataField]
    public int MinLine = 3;

    [DataField]
    public float LineSpacing = 1.5f;

    [DataField]
    public TimeSpan HumanCopyDelay { get; set; } = TimeSpan.FromSeconds(600);

    [DataField]
    public TimeSpan NextHumanCopy { get; set; } = TimeSpan.Zero;

    [DataField]
    public TimeSpan CleaningDelay { get; set; } = TimeSpan.FromSeconds(300);

    [DataField]
    public TimeSpan NextCleaning { get; set; } = TimeSpan.Zero;

    [DataField]
    public EntProtoId ClonePrototype = "DistortedCloneSpawner";

    [DataField]
    public EntityUid? RealGrid = null;

    [DataField]
    public AlternateDimensionConfig? DimensionType = null;
}
