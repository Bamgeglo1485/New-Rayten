
using Content.Shared.Access;
using Robust.Shared.Prototypes;

namespace Content.Shared.Vanilla.Entities.ArsenalAuthorizator;

[RegisterComponent]
public sealed partial class ArsenalDoorComponent : Component
{
    /// <summary>
    /// Доступ будет добавляться и убираться у двери, запрещая проходить мобам через неё
    /// </summary>
    [DataField]
    public ProtoId<AccessLevelPrototype> BlockAccess = "Security";
}
