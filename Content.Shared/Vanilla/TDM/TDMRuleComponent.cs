using Content.Shared.FixedPoint;
using Content.Shared.Roles;
using Content.Shared.Storage;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.GameStates;
using Robust.Shared.Map;

namespace Content.Shared.Vanilla.TDM;

[RegisterComponent]
public sealed partial class TDMRuleComponent : Component
{
    [DataField]
    public TimeSpan NextUpdate;

    [DataField]
    public TimeSpan TimeOnNewCycle = TimeSpan.FromSeconds(0);

    [DataField]
    public TimeSpan TimeToNewCycle = TimeSpan.FromSeconds(200);

    [DataField]
    public TimeSpan TimeForPlayersJoin = TimeSpan.FromMinutes(1f);

    /// <summary>
    /// игроки, которые будут учавствовать в пвп
    /// </summary>
    [DataField]
    public HashSet<ICommonSession> Players = new();

    [DataField]
    public int Playercount = 0;
    /// <summary>
    /// Словарь игровых персонажей и к какой команде они относятся
    /// </summary>
    [DataField]
    public Dictionary<EntityUid, bool> PlayerCharacters = new();

    /// <summary>
    /// Последний раунд, после него рестартим сервер
    /// </summary>
    [DataField]
    public bool LastRound = false;

    [DataField]
    public TDMStatus CurrentStatus = TDMStatus.awaitstart;

    [DataField]
    public bool Firstblooded = false;

    [DataField]
    public EntityUid Arena;

    [DataField]
    public MapId? ArenaMapId = null;

    [DataField]
    public TDMMapPrototype? TDMProto = null;

    public bool NextTeam = false;
}

public enum TDMStatus : byte
{
    /// <summary>
    /// Спавн новой арены, сбор желающих на участие
    /// </summary>
    awaitstart = 1,

    /// <summary>
    /// Спавн всех игроков на арене, новые игроки не могут подключиться, все зафрижены
    /// </summary>
    startup = 1,

    /// <summary>
    /// обратный отсчёт до разморозки
    /// </summary>
    countdown = 2,

    /// <summary>
    /// разморозка
    /// </summary>
    unfreeze = 3,

    /// <summary>
    /// Раунд начат, 5 минут до конца
    /// </summary>
    started = 4,

    /// <summary>
    /// Раунд окончен
    /// </summary>
    ended = 5
}
