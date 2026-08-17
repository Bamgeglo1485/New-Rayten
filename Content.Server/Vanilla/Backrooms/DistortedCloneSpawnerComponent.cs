using Content.Shared.Cloning;
using Robust.Shared.Prototypes;

namespace Content.Server.Vanilla.Backrooms;

[RegisterComponent, EntityCategory("Spawner")]
public sealed partial class DistortedCloneSpawnerComponent : Component
{
    [DataField]
    public ProtoId<CloningSettingsPrototype> Settings = "BaseClone";

    [DataField]
    public float AgressiveChance = 0.5f;

    [DataField]
    public ComponentRegistry Components { get; private set; } = new();
}
