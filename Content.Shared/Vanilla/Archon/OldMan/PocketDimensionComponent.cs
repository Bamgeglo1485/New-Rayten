using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Robust.Shared.Serialization;
namespace Content.Shared.Vanilla.Archon.OldMan;

[RegisterComponent]
public sealed partial class PocketDimensionComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan TeleportsSpawnInterval = TimeSpan.FromSeconds(60);

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan NextSpawn;

    [DataField]
    public string TeleportPrototype = "PocketDimensionExitTeleport";

    [DataField]
    public string FakeTeleportPrototype = "PocketDimensionExitTeleportFake";

    [DataField]
    public int TeleportsAmount = 3;

    [DataField]
    public int FakeTeleportsAmount = 6;
}
