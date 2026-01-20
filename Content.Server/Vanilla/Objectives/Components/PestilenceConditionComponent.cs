using Content.Server.Vanilla.Objectives.Systems;

namespace Content.Server.Vanilla.Objectives.Components;

[RegisterComponent, Access(typeof(PestilenceConditionSystem))]
public sealed partial class PestilenceConditionComponent : Component
{
    /// <summary>
    /// удалось ли спасти мир
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public bool IsOver = false;
}