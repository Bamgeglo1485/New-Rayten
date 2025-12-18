using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Robust.Shared.Serialization;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Robust.Shared.Audio;
using Robust.Shared.Map;

namespace Content.Shared.Vanilla.Archon.OldMan;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class OldManComponent : Component
{
    [AutoNetworkedField]
    [DataField]
    public EntityUid? Target = new();

    [ViewVariables(VVAccess.ReadWrite)]
    public EntityCoordinates TeleportCoordinates { get; set; }

    // Находится ли он в карманном измерении. Ну то есть в бездействии
    [AutoNetworkedField]
    [DataField]
    public bool InDimension = true;

    [DataField]
    public bool AnimationShown = false;

    // Время начала анимации
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoNetworkedField, AutoPausedField]
    public TimeSpan AnimationStartTime = TimeSpan.Zero;

    // Длительность выжидания цели в карманном измерении
    [DataField]
    public float MinWaitTime = 120;

    [DataField]
    public float MaxWaitTime = 400;

    [DataField]
    public float RepeatedWaitTime = 10;

    [DataField]
    public string SpawnAnimation = "OldManSpawn";

    [DataField]
    public TimeSpan SpawnAnimationDelay = TimeSpan.FromSeconds(3);

    [DataField]
    public string DespawnAnimation = "OldManDespawn";

    // Длительность погони за одной целью
    [DataField]
    public TimeSpan ChaseDelay = TimeSpan.FromSeconds(60);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoNetworkedField, AutoPausedField]
    public TimeSpan NextChaseStart = TimeSpan.Zero;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoNetworkedField, AutoPausedField]
    public TimeSpan ChaseEnd = TimeSpan.Zero;

    [DataField]
    public SoundSpecifier? SpawnSound = new SoundPathSpecifier("/Audio/Vanilla/Effects/Archon/106spawn.ogg");

    [DataField]
    public SoundSpecifier? DimensionTeleportSound = new SoundPathSpecifier("/Audio/Vanilla/Effects/Archon/106laugh1.ogg");

    [DataField]
    public ResPath DimensionMap = new ResPath("/Maps/Vanilla/Misc/PocketDimension.yml");

    [DataField]
    public EntityUid? DimensionUid = new();
}
