using Content.Shared.FixedPoint;
using Content.Shared.Damage;
using Content.Shared.Random;
using Robust.Shared.GameStates;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared.Vanilla.Archon.OldMan;

[RegisterComponent, NetworkedComponent]
public sealed partial class DimensionVictimComponent : Component
{
    /// <summary>
    /// порталы заспавненные на эту жертву
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public HashSet<EntityUid> Portals = [];
    /// <summary>
    /// Грид карманного измерения
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public EntityUid DimensionGridUid = default;
    /// <summary>
    /// дедус
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public Entity<OldManComponent> OldMan = default;

    [DataField]
    public string TeleportPrototype = "PocketDimensionExitTeleport";

    [DataField]
    public string FakeTeleportPrototype = "PocketDimensionExitTeleportFake";
    [DataField]
    public ProtoId<WeightedRandomPrototype> DeadResults = "DimnsionVictimResults";
    /// <summary>
    /// такое количество телепортов заспавнится на одну жертву
    /// </summary>
    [DataField]
    public int TeleportsAmount = 1;
    /// <summary>
    /// такое количество фейковых телепортов заспавнится на одну жертву
    /// </summary>
    [DataField]
    public int FakeTeleportsAmount = 5;

    [DataField]
    public SoundSpecifier DimensionEscapeSound = new SoundPathSpecifier("/Audio/Vanilla/Effects/Archon/106/106ExitPD.ogg");

    [DataField]
    public SoundSpecifier DimensionEnterSound = new SoundPathSpecifier("/Audio/Vanilla/Effects/Archon/106/106EnterPD.ogg");

    [DataField]
    public SoundSpecifier DamageSound = new SoundCollectionSpecifier("106corrosion");

    [DataField]
    public SoundSpecifier DimensionAmbient = new SoundPathSpecifier("/Audio/Vanilla/Ambience/106/106dimension.ogg", AudioParams.Default.WithLoop(true));
    [ViewVariables]
    public EntityUid? Stream = null;

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan DamageInterval = TimeSpan.FromSeconds(10);

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan NextDamage;

    [DataField]
    [ViewVariables(VVAccess.ReadWrite)]
    public DamageSpecifier Damage = new()
    {
        DamageDict = new Dictionary<string, FixedPoint2>
        {
            ["Caustic"] = 5,
            ["Cellular"] = 0.1
        }
    };
}
[RegisterComponent, NetworkedComponent]
public sealed partial class OldManFoodComponent : Component
{
}