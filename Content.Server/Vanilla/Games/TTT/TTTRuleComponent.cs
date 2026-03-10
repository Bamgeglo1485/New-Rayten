using Content.Shared.Roles;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Map;
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
    public SoundSpecifier InoBrief = new SoundPathSpecifier("/Audio/Vanilla/Effects/TTT/innocentbrief.ogg");
    [DataField]
    public SoundSpecifier DecBrief = new SoundPathSpecifier("/Audio/Vanilla/Effects/TTT/decbrief.ogg");
    [DataField]
    public SoundSpecifier AwaitRolesMusic = new SoundPathSpecifier("/Audio/Vanilla/StationEvents/Forever_Blowing_Bubbles.ogg",
            AudioParams.Default.WithVolume(-6f));

    [DataField]
    public TimeSpan TimeOnNewCycle = TimeSpan.FromSeconds(0);

    [DataField]
    public TimeSpan TimeToNewCycle = TimeSpan.FromSeconds(480);

    [DataField]
    public TimeSpan TimeForPlayersJoin = TimeSpan.FromMinutes(1f);
    [DataField]
    public List<ProtoId<StartingGearPrototype>> StartingGear = new()
    {
        "TTTGearInnocent"
    };



    [ViewVariables]
    public HashSet<ICommonSession> Sessions = [];
    [ViewVariables]
    public TimeSpan NextUpdate;
    [ViewVariables]
    public int InoCount = 0;
    [ViewVariables]
    public int TraitorsCount = 0;
    [ViewVariables]
    public TTTStatus CurrentStatus = TTTStatus.AwaitStart;

    [ViewVariables]
    public EntityUid Arena = default;

    [ViewVariables]
    public MapId ArenaMapId = default;

    [ViewVariables]
    public TDMMapPrototype? TDMProto = null;
    [ViewVariables]
    public int Announcments = 0;
}

public enum TTTStatus : byte
{
    /// <summary>
    /// Спавн новой арены, сбор желающих на участие
    /// </summary>
    AwaitStart = 1,

    /// <summary>
    /// Спавн всех игроков на арене, новые игроки не могут подключиться, роли не распределены
    /// </summary>
    Startup = 2,

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
    Ended = 5
}
