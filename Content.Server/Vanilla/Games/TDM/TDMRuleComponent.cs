using Robust.Shared.Player;
using Robust.Shared.Map;
using Robust.Shared.Audio;

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
    [DataField]
    public SoundSpecifier CountDownSound = new SoundPathSpecifier("/Audio/Vanilla/Effects/TDM/counting.ogg");
    [DataField]
    public SoundSpecifier FirstBloodSound = new SoundPathSpecifier("/Audio/Vanilla/Effects/TDM/Firstblood.ogg");
    public Dictionary<int, SoundPathSpecifier> KillSounds = new()
    {
        { 2, new SoundPathSpecifier("/Audio/Vanilla/Effects/TDM/Doublekill.ogg") },
        { 3, new SoundPathSpecifier("/Audio/Vanilla/Effects/TDM/TripleKill.ogg")  },
        { 4, new SoundPathSpecifier("/Audio/Vanilla/Effects/TDM/UltraKill.ogg")  },
        { 5, new SoundPathSpecifier("/Audio/Vanilla/Effects/TDM/Rampage.ogg")  },
    };
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
