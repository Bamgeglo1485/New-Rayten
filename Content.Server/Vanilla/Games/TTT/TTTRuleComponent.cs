using Content.Shared.FixedPoint;
using Content.Shared.Roles;
using Content.Shared.Storage;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Content.Shared.Vanilla.Games.TTT;
using Content.Shared.Vanilla.TDM;
using Robust.Shared.Audio;
namespace Content.Server.Vanilla.Games.TTT;

[RegisterComponent]
public sealed partial class TTTRuleComponent : Component
{
    [DataField]
    public SoundSpecifier WinSound = new SoundPathSpecifier("/Audio/Vanilla/Effects/TTT/winsound.ogg");
    [DataField]
    public SoundSpecifier LoseSound = new SoundPathSpecifier("/Audio/Vanilla/Effects/TTT/losesound.ogg");
    [DataField]
    public SoundSpecifier TraitorBrief = new SoundPathSpecifier("/Audio/Ambience/Antag/traitor_start.ogg");
    [DataField]
    public SoundSpecifier InoBrief = new SoundPathSpecifier("/Audio/Ambience/Antag/traitor_start.ogg");
    [DataField]
    public SoundSpecifier DecBrief = new SoundPathSpecifier("/Audio/Vanilla/Effects/TTT/decbrief.ogg");



    [DataField]
    public TimeSpan NextUpdate;

    [DataField]
    public TimeSpan TimeOnNewCycle = TimeSpan.FromSeconds(0);

    [DataField]
    public TimeSpan TimeToNewCycle = TimeSpan.FromSeconds(480);

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
    public Dictionary<EntityUid, TTTRole> PlayerCharacters = new();


    [DataField]
    public TTTStatus CurrentStatus = TTTStatus.awaitstart;

    [DataField]
    public EntityUid Arena;

    [DataField]
    public MapId? ArenaMapId = null;

    [DataField]
    public TDMMapPrototype? TDMProto = null;
    public int Anoncments = 0;
}

public enum TTTStatus : byte
{
    /// <summary>
    /// Спавн новой арены, сбор желающих на участие
    /// </summary>
    awaitstart = 1,

    /// <summary>
    /// Спавн всех игроков на арене, новые игроки не могут подключиться, роли не распределены
    /// </summary>
    startup = 2,

    /// <summary>
    /// Выдача ролек
    /// </summary>
    AwaitRolesToAdd = 3,

    /// <summary>
    /// Раунд начат
    /// </summary>
    RoundInProgress = 4,

    /// <summary>
    /// Раунд окончен
    /// </summary>
    ended = 5
}
