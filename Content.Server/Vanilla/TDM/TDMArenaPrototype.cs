using Robust.Shared.Prototypes;
using Content.Shared.Roles;
namespace Content.Server.Vanilla.TDM;

[Prototype("TDMMap")]
public sealed partial class TDMMapPrototype : IPrototype
{
    /// <summary>
    /// айди карты
    /// </summary>
    [IdDataField]
    public string ID { get; } = default!;

    [ViewVariables]
    /// <summary>
    /// Путь до карты
    /// </summary>
    [DataField("arena")]
    public string ArenaPath = "Maps/Vanilla/Knocker_ERT_Base.yml";

    /// <summary>
    /// количество игроков на карте
    /// </summary>
    [DataField("arenaparty")]
    public int ArenaParty = 20;
    /// <summary>
    /// Экипировка синей команды
    /// </summary>
    [DataField("blueteamgear")]
    public List<ProtoId<StartingGearPrototype>> BlueTeamGear = new();
    /// <summary>
    /// Экипировка красной команды
    /// </summary>
    [DataField("redteamgear")]
    public List<ProtoId<StartingGearPrototype>> RedTeamGear = new();
}
