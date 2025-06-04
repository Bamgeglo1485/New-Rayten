using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared.Humanoid.Prototypes;
using Content.Corvax.Interfaces.Shared;
using Content.Shared.Random;
using Content.Shared.Vanilla.Sponsor;
using Robust.Shared.Collections;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;
using Content.Shared.Preferences;
using Content.Shared.Vanilla.Skill;

namespace Content.Shared.Vanilla.Background;


[Serializable, NetSerializable, DataDefinition]
public sealed partial class RoleBackground : IEquatable<RoleBackground>
{

    [DataField]
    public ProtoId<RoleBackgroundPrototype> Role;

    [DataField]
    public ProtoId<BackgroundPrototype>? SelectedBabyBackground;
    [DataField]
    public ProtoId<BackgroundPrototype>? SelectedAdultBackground;
    [DataField]
    public ProtoId<BackgroundPrototype>? SelectedGeneralBackground;

    public int SkillpointCredit = 0;

    [DataField]
    public Dictionary<skillType, SkillLevel> AddedBasicSkills = new();
    [DataField]
    public HashSet<skillType> AddedEasySkills = new();

    public RoleBackground(ProtoId<RoleBackgroundPrototype> role)
    {
        Role = role;
    }

    public RoleBackground Clone()
    {
        return new RoleBackground(Role)
        {
            SkillpointCredit = SkillpointCredit,
            SelectedBabyBackground = SelectedBabyBackground,
            SelectedAdultBackground = SelectedAdultBackground,
            SelectedGeneralBackground = SelectedGeneralBackground,
            AddedBasicSkills = new Dictionary<skillType, SkillLevel>(AddedBasicSkills),
            AddedEasySkills = new HashSet<skillType>(AddedEasySkills),
        };
    }


    public void EnsureValid(HumanoidCharacterProfile profile, ICommonSession session, IDependencyCollection collection)
    {
        var protoManager = collection.Resolve<IPrototypeManager>();
        var sponsors = collection.Resolve<SharedSponsorManager>();
        var netManager = collection.Resolve<INetManager>(); // Corvax-Loadouts
        string[] sponsorPrototypes;

        if (netManager.IsServer)
            sponsorPrototypes = sponsors.TryGetServerPrototypes(session.UserId, out var prototypes) ? prototypes.ToArray() : [];
        else
            sponsorPrototypes = sponsors.GetClientPrototypes().ToArray();

        List<(skillType Skill, SkillLevel Level, int Experience)> generalbasicskills = new List<(skillType Skill, SkillLevel Level, int Experience)>
        {
            (skillType.RangeWeapon, SkillLevel.None, 0),
            (skillType.MeleeWeapon, SkillLevel.None, 0),
            (skillType.Medicine, SkillLevel.None, 0),
            (skillType.Chemistry, SkillLevel.None, 0),
            (skillType.Engineering, SkillLevel.None, 0),
            (skillType.Building, SkillLevel.None, 0),
            (skillType.Research, SkillLevel.None, 0),
            (skillType.Crime, SkillLevel.None, 0)
        };
        List<(skillType Skill, bool have, int Experience)> generaleasyskills = new List<(skillType Skill, bool have, int Experience)>
        {
            (skillType.Piloting, false, 0),
            (skillType.Botany, false, 0),
            (skillType.MusInstruments, false, 0),
            (skillType.Bureaucracy, false, 0),
            (skillType.Atmosphere, false, 0)
        };
        int skillpoints = 0;

        // 1. Проверка, что роль существует
        if (!protoManager.TryIndex(Role, out RoleBackgroundPrototype? roleProto))
        {
            SetDefault(profile);
            return;
        }

        // 2. Проверка предысторий по типу
        bool ValidateBackground(ref ProtoId<BackgroundPrototype>? selectedBackgroundId, string? groupId, BackgroundGroupType requiredType)
        {
            if (selectedBackgroundId == null || string.IsNullOrEmpty(groupId))
            {
                Logger.Error("пустая предыстория");
                return false;
            }

            // Проверка, что группа существует
            if (!protoManager.TryIndex<BackgroundGroupPrototype>(groupId, out var groupProto))
            {
                Logger.Error("группы не существует");
                return false;
            }

            // Проверка, что группа соответствует типу
            if (groupProto.Type != requiredType)
            {
                Logger.Error("группа не соответствует типу");
                return false;
            }

            // Проверка, что предыстория существует
            if (!protoManager.TryIndex<BackgroundPrototype>(selectedBackgroundId.Value, out var backgroundProto))
            {
                Logger.Error("предыстории не существует");
                return false;
            }

            // Проверка, что предыстория входит в группу
            if (!groupProto.Backgrounds.Contains(selectedBackgroundId.Value))
            {
                Logger.Error("предыстория не подходит группе");
                return false;
            }

            // Проверка на донат
            if (backgroundProto.SponsorOnly && !sponsorPrototypes.Contains(backgroundProto.ID))
            {
                Logger.Error($"ЭЭЭ ТЫ НЕ ДОНАТЕР, тебе нельзя использовать {backgroundProto.ID}");
                return false;
            }

            return true;
        }
        // 3. Проверка на навыки
        //Расчитывает скиллпоинты, если при пересечении навыков из разных предысторий уровень превышает максимальный
        void CalculateCreditFromBasicSkills(Dictionary<skillType, SkillLevel>? backgroundSkills)
        {
            if (backgroundSkills == null)
                return;

            foreach (var (skill, level) in backgroundSkills)
            {
                int index = generalbasicskills.FindIndex(s => s.Skill == skill);
                if (index == -1)
                    continue;

                var current = generalbasicskills[index];
                int total = (int)current.Level + (int)level;
                SkillLevel newLevel = total > (int)SkillLevel.Expert
                    ? SkillLevel.Expert
                    : (SkillLevel)total;

                if (total > (int)SkillLevel.Expert)
                    skillpoints += total - (int)SkillLevel.Expert;

                generalbasicskills[index] = (skill, newLevel, 0);
            }
        }
        void CalculateCreditFromEasySkills(HashSet<skillType>? backgroundSkills)
        {
            if (backgroundSkills == null)
                return;

            foreach (var skill in backgroundSkills)
            {
                int index = generaleasyskills.FindIndex(s => s.Skill == skill);
                if (index == -1)
                    continue;

                var current = generaleasyskills[index];
                if (current.have)
                {
                    skillpoints += 1;
                }
                else
                {
                    generaleasyskills[index] = (skill, true, 0);
                }
            }
        }
        void CalculateCreditFromAdditiveBasic(Dictionary<skillType, SkillLevel>? backgroundSkills)
        {
            if (backgroundSkills == null)
                return;

            foreach (var (_, level) in backgroundSkills)
            {
                skillpoints -= (int)level;
            }
        }
        void CalculateCreditFromAdditiveEasy(HashSet<skillType>? backgroundSkills)
        {
            if (backgroundSkills == null)
                return;

            skillpoints -= backgroundSkills.Count;
        }


        // 4. Проверка что выбрана либо одна основная предыстория либо детская + взрослая
        var useSplitBackgrounds = SelectedGeneralBackground == null &&
                                  SelectedAdultBackground != null &&
                                  SelectedBabyBackground != null;

        var useGeneralBackground = SelectedGeneralBackground != null &&
                                   SelectedAdultBackground == null &&
                                   SelectedBabyBackground == null;

        if (useSplitBackgrounds)
        {
            //Валидируем предыстории
            if (!ValidateBackground(ref SelectedBabyBackground, roleProto.Baby, BackgroundGroupType.Baby) ||
                !ValidateBackground(ref SelectedAdultBackground, roleProto.Adult, BackgroundGroupType.Adult))
            {
                SetDefault(profile);
                return;
            }


            //Валидируем навыки


            if (protoManager.TryIndex(SelectedBabyBackground, out var bgProtoBaby) &&
                protoManager.TryIndex(SelectedAdultBackground, out var bgProtoAdult))
            {
                //Добавляем дополнительные скиллпоинты от предысторий
                skillpoints += bgProtoBaby.SkillPoints;
                skillpoints += bgProtoAdult.SkillPoints;
                //считаем кредиты от пересечения лёгких навыков
                CalculateCreditFromEasySkills(bgProtoBaby.EasySkills);
                CalculateCreditFromEasySkills(bgProtoAdult.EasySkills);

                //считаем кредиты от пересечения основных навыков
                CalculateCreditFromBasicSkills(bgProtoBaby.Skills);
                CalculateCreditFromBasicSkills(bgProtoAdult.Skills);
                if (skillpoints != SkillpointCredit)
                {
                    SetDefault(profile);
                    return;
                }
                //считаем что выбранные навыки соответствуют кредиту
                CalculateCreditFromAdditiveBasic(AddedBasicSkills);
                CalculateCreditFromAdditiveEasy(AddedEasySkills);
                if (skillpoints != 0)
                {
                    SetDefault(profile);
                    return;
                }
                //считаем что выбранные навыки не увеличивают навык сверх максимума
                CalculateCreditFromBasicSkills(AddedBasicSkills);
                CalculateCreditFromEasySkills(AddedEasySkills);
                if (skillpoints != 0)
                {
                    SetDefault(profile);
                    return;
                }
            }
            else
            {
                SetDefault(profile);
                return;
            }
        }
        else if (useGeneralBackground)
        {
            //Валидируем предысторию
            if (!ValidateBackground(ref SelectedGeneralBackground, roleProto.General, BackgroundGroupType.General))
            {
                SetDefault(profile);
                return;
            }

            //Валидируем навыки

            if (protoManager.TryIndex(SelectedGeneralBackground, out var bgProtoGenerl))
            {
                //Добавляем дополнительные скиллпоинты от предысторий
                skillpoints += bgProtoGenerl.SkillPoints;
                //считаем кредиты от пересечения лёгких навыков
                CalculateCreditFromEasySkills(bgProtoGenerl.EasySkills);

                //считаем кредиты от пересечения основных навыков
                CalculateCreditFromBasicSkills(bgProtoGenerl.Skills);
                if (skillpoints != SkillpointCredit)
                {
                    Logger.Error("скиллпоинты не соответствуют кредиту");
                    SetDefault(profile);
                    return;
                }
                //считаем что выбранные навыки соответствуют кредиту
                CalculateCreditFromAdditiveBasic(AddedBasicSkills);
                CalculateCreditFromAdditiveEasy(AddedEasySkills);

                if (skillpoints != 0)
                {
                    Logger.Error("навыки не соответствуют кредиту");
                    SetDefault(profile);
                    return;
                }
                //считаем что выбранные навыки не увеличивают навык сверх максимума
                CalculateCreditFromBasicSkills(AddedBasicSkills);
                CalculateCreditFromEasySkills(AddedEasySkills);

                if (skillpoints != 0)
                {
                    Logger.Error("навыки добавлены сверх максимума");
                    SetDefault(profile);
                    return;
                }
            }
            else
            {
                Logger.Error("не удалось индексировать предысторию");
                SetDefault(profile);
                return;
            }
        }
        else
        {
            Logger.Error("не выбрана одна общая либо детская + взрослая предытория");
            SetDefault(profile);
            return;
        }
    }

    public void SetDefault(HumanoidCharacterProfile? profile)
    {
        if (profile == null)
            return;
        SelectedBabyBackground = null;
        SelectedAdultBackground = null;
        SelectedGeneralBackground = null;
        SkillpointCredit = 0;
        AddedBasicSkills = new();
        AddedEasySkills = new();
    }


    // /// <summary>
    // /// Returns whether a loadout is valid or not.
    // /// </summary>
    public bool IsValid(HumanoidCharacterProfile profile, ICommonSession? session, ProtoId<BackgroundPrototype> background, IDependencyCollection collection, [NotNullWhen(false)] out FormattedMessage? reason)
    {
        reason = null;

        var protoManager = collection.Resolve<IPrototypeManager>();

        if (!protoManager.TryIndex(background, out var backgroundProto))
        {
            // Uhh
            reason = FormattedMessage.FromMarkupOrThrow("");
            return false;
        }

        if (!protoManager.HasIndex(Role))
        {
            reason = FormattedMessage.FromUnformatted("backgrounds-prototype-missing");
            return false;
        }

        var valid = true;

        // foreach (var effect in loadoutProto.Effects)
        // {
        //     valid = valid && effect.Validate(profile, this, loadoutProto, session, collection, out reason);
        // }

        return valid;
    }


    public bool Equals(RoleBackground? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;

        return Role.Equals(other.Role)
            && Equals(SelectedBabyBackground, other.SelectedBabyBackground)
            && Equals(SelectedAdultBackground, other.SelectedAdultBackground)
            && Equals(SelectedGeneralBackground, other.SelectedGeneralBackground)
            && SkillpointCredit == other.SkillpointCredit
            && AddedBasicSkills.Count == other.AddedBasicSkills.Count
            && AddedBasicSkills.All(kv => other.AddedBasicSkills.TryGetValue(kv.Key, out var val) && val == kv.Value)
            && AddedEasySkills.SetEquals(other.AddedEasySkills);
    }


    public override bool Equals(object? obj)
    {
        return obj is RoleBackground other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Role, SelectedBabyBackground, SelectedAdultBackground, SelectedGeneralBackground);
    }
}
