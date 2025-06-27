using Content.Server.GameTicking.Rules.Components;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Robust.Shared.Map;
using Content.Shared.Weather;

namespace Content.Server.Vanilla.GameTicking.Rules.WhiteOut;

[RegisterComponent, Access(typeof(WhiteoutRuleSystem))]
public sealed partial class WhiteoutRuleComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public bool PlanetMap = false;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float WhiteoutLength = 900;
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float WhiteoutFinalLength = 300f;
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float WhiteoutPrepareTime = 3000f;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float WhiteoutPrepareTemp = 213.15f;
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float WhiteoutTemp = 123.15f;
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float WhiteoutFinalTemp = 23.15f;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float WhiteoutPrepareStrength = 0.008f;
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float WhiteoutStrength = 0.030f;
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float WhiteoutFinalModifier = 1.5f;


    [DataField]
    public string PrestartWeather = "SnowfallMedium";
    [DataField]
    public string Weather = "SnowfallHeavy";

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public LocId WhiteoutPrepareAnnouncement = "whiteout-prepare-announcement";
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public LocId WhiteoutPrestartAnnouncement = "whiteout-prestart-announcement";
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public LocId WhiteoutAnnouncement = "whiteout-announcement";
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public LocId WhiteoutFinalAnnouncement = "whiteout-announcement-final";
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public LocId WhiteoutEndAnnouncement = "whiteout-announcement-end";

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public SoundSpecifier WhiteoutSoundAnnouncement = new SoundPathSpecifier("/Audio/Vanilla/StationEvents/announcement.ogg");
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public SoundSpecifier WhiteoutAlarmSound = new SoundPathSpecifier("/Audio/Vanilla/StationEvents/whiteout_siren.ogg");

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public SoundSpecifier WhiteoutMusic = new SoundPathSpecifier("/Audio/Vanilla/StationEvents/whiteout.ogg");
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public SoundSpecifier WhiteoutPrestartMusic = new SoundPathSpecifier("/Audio/Vanilla/StationEvents/whiteout_prestart.ogg");
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public SoundSpecifier WhiteoutFinalMusic = new SoundPathSpecifier("/Audio/Vanilla/StationEvents/whiteout_final.ogg");

    public float TimeActive;
    public TimeSpan NextUpdate;
    public WhiteoutState CurrentState = WhiteoutState.Ended;
    public MapId ActiveMapId = MapId.Nullspace;
    public MapId LondonersMapId = MapId.Nullspace;
    public EntityUid LondonersMapUid = EntityUid.Invalid;
    public EntityUid ActiveMapUid = EntityUid.Invalid;
    public TimeSpan NextGlassBreak;
    public bool PrestartPlayed = false;

    /// <summary>
    /// Вычисляет что-то я хз
    /// </summary>
    public (float Temp, float Strength) GetWhiteoutParams(bool isFinal)
    {
        var duration = WhiteoutLength + WhiteoutFinalLength;
        var strengthFactor = WhiteoutStrength * (TimeActive / duration);

        if (isFinal)
            return (WhiteoutFinalTemp, strengthFactor * WhiteoutFinalModifier);
        else
            return (WhiteoutTemp, strengthFactor);
    }
}
public enum WhiteoutState : byte
{
    Preparing = 0,
    Active = 1,
    FinalPhase = 3,
    Ended = 4,
}
