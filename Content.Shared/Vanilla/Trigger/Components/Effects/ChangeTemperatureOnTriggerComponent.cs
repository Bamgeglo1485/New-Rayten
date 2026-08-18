using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.Trigger.Components.Effects;

[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class ChangeTemperatureOnTriggerComponent : BaseXOnTriggerComponent
{
    /// <summary>
    /// The amount it changes the target's temperature by. In Joules.
    /// </summary>
    [DataField]
    public float Heat = 0f;

    /// <summary>
    /// If this heat change ignores heat resistance or not.
    /// </summary>
    [DataField]
    public bool IgnoreHeatResistance = true;
}
