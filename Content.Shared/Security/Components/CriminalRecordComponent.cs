using Content.Shared.StatusIcon;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Security.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CriminalRecordComponent : Component
{
    /// <summary>
    ///     The icon that should be displayed based on the criminal status of the entity.
    /// </summary>
    [DataField, AutoNetworkedField]
    public ProtoId<SecurityIconPrototype> StatusIcon = "SecurityIconWanted";

    //rayten-start
    [DataField, AutoNetworkedField]
    public SecurityStatus Status;
    public bool SecuritronAgro => Status switch
    {
        SecurityStatus.Wanted => true, // в розыске
        SecurityStatus.Detained => true, // под арестом
        _ => false
    };
    //rayten-end
}
