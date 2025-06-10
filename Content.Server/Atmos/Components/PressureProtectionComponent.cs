using Content.Server.Atmos.EntitySystems;

using Content.Server.Vanilla.GameTicking.Rules.WhiteOut; // VANILLA WHITEOUT

namespace Content.Server.Atmos.Components;

[RegisterComponent]
[Access(typeof(BarotraumaSystem), typeof(WhiteoutRuleSystem))] // VANILLA WHITEOUT
public sealed partial class PressureProtectionComponent : Component
{
    [DataField]
    public float HighPressureMultiplier = 1f;

    [DataField]
    public float HighPressureModifier;

    [DataField]
    public float LowPressureMultiplier = 1f;

    [DataField]
    public float LowPressureModifier;
}

/// <summary>
/// Event raised on an entity with <see cref="PressureProtectionComponent"/> in order to adjust its default values.
/// </summary>
[ByRefEvent]
public record struct GetPressureProtectionValuesEvent
{
    public float HighPressureMultiplier;
    public float HighPressureModifier;
    public float LowPressureMultiplier;
    public float LowPressureModifier;
}

