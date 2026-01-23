using Content.Shared.Damage;
using Robust.Shared.GameStates;

namespace Content.Shared.Vanilla.Archon.ContainerPunishment;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ContainerPunishmentComponent : Component
{
    /// <summary>
    /// Словарь, хранящий счетчики всех кто брал предмет из контейнера
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public Dictionary<EntityUid, int> Counters = [];
    /// <summary>
    /// количество предметов которые можно вытащить
    /// </summary>
    [DataField]
    public int MaxItems = 2;

    [DataField]
    public int BaseItemsToResearch = 5;

    [DataField]
    public int ItemsToResearch = 5;

    /// <summary>
    /// Урон который будет нанесён
    /// </summary>
    [DataField, AutoNetworkedField]
    public DamageSpecifier Damage = new()
    {
        DamageDict = new()
        {
            { "Slash", 30 }
        }
    };
}