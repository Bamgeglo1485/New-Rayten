using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Audio;
namespace Content.Shared.Vanilla.Skill;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SkillComponent : Component
{
    /// <summary>
    /// очки навыков
    /// </summary>
    [DataField, AutoNetworkedField]
    public int SkillPoints { get; set; } = 0;

    [DataField, AutoNetworkedField]
    public SoundSpecifier UnSkillSound = new SoundPathSpecifier("/Audio/Vanilla/SkillSystem/meep-merp.ogg");

    /// <summary>
    /// основные навыки, которыми обладает сущность
    /// </summary>
    [DataField, AutoNetworkedField]
    public Dictionary<SkillType, SkillLevel> BasicSkills = [];
    /// <summary>
    /// лёгкие навыки, которым обладает сущность
    /// </summary>
    [DataField, AutoNetworkedField]
    public HashSet<SkillType> EasySkills = [];

    /// <summary>
    /// Опыт навыков
    /// </summary>
    [DataField, AutoNetworkedField]
    public Dictionary<SkillType, int> SkillExps = [];

}

/// <summary>
/// навыки
/// </summary>
[Serializable, NetSerializable]
public enum SkillType : byte
{
    Weapon,
    Medicine,
    Engineering,
    Piloting,
    Research,
    MusInstruments,
    Botany,
    Bureaucracy
}

/// <summary>
/// уровни навыков
/// </summary>
[Serializable, NetSerializable]
public enum SkillLevel
{
    None = 1,
    Basic = 2,
    Advanced = 3,
    Expert = 4
}