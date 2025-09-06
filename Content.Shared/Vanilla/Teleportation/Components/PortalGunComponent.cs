using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;
using Robust.Shared.GameStates;

namespace Content.Shared.Teleportation.Components;

/// <summary>
///     Давай морти приключение на 20 минут
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PortalGunComponent : Component
{

    /// <summary>
    ///     Небходимый реагент
    /// </summary>
    [DataField, AutoNetworkedField]
    public ProtoId<ReagentPrototype> ReagentName = "PortalJuice";

    /// <summary>
    ///     Название ёмкости
    /// </summary>
    [DataField, AutoNetworkedField]
    public string SolutionName = "portal";

    /// <summary>
    ///     Скорость проджектайла
    /// </summary>
    [DataField("projectileVelocity")]
    public float ProjectileVelocity = 25f;
}
