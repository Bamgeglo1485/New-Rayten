using Content.Shared.Vanilla.Skill;
using Content.Shared.SkillTrainer;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Robust.Shared.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server.SkillTrainer;

public sealed class ServerSkillTrainerSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    const int _EXPERIENCEFROMSKILLPOINT = 600;
    const int _EXPERIENCETONEWLVL = 600;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<UseSkillPointEvent>(UseSkillPoint);
    }

    private void UseSkillPoint(UseSkillPointEvent msg, EntitySessionEventArgs args)
    {
        // Проверяем, что у пользователя есть прикрепленное существо, и что навык задан
        if (!args.SenderSession.AttachedEntity.HasValue || msg.skill == null)
            return;

        var entity = args.SenderSession.AttachedEntity.Value;

        // Получаем компонент навыков
        if (!EntityManager.TryGetComponent<SkillComponent>(entity, out var skillComp) || skillComp.SkillPoints < 1)
            return;

        //проверка если нам ваще гавно какое-то пришло которое невозможно никак определить
        if (skillComp.GetSkillLevel(msg.skill)==null && skillComp.GetEasySkill(msg.skill)==null)
            return;

        //проверка основных скилов
        if (skillComp.GetSkillLevel(msg.skill)!=null && skillComp.GetSkillLevel(msg.skill) >= SkillLevel.Expert)
            return;

        //проверка легких скилов
        if (skillComp.GetEasySkill(msg.skill)!=null && skillComp.GetEasySkill(msg.skill) == true)
            return;

        // Уменьшаем очки навыков
        skillComp.SkillPoints--;
        skillComp.Dirty();

        // Добавляем опыт
        AddExperience(skillComp, msg.skill, _EXPERIENCEFROMSKILLPOINT, multiplyed: false, player: args.SenderSession);
    }

    public bool AddExperience(SkillComponent skillComp, skillType skillType, int experienceAmount, bool multiplyed = true, ICommonSession? player = null)
    {
        if (multiplyed && (int)skillComp.ResearchLevel == 3)
            experienceAmount*=2;

        if (skillType == skillType.Piloting 
        || skillType == skillType.MusInstruments 
        || skillType == skillType.Botany 
        || skillType == skillType.Bureaucracy 
        || skillType == skillType.Thief 
        || skillType == skillType.Stealth) 
        {
            bool? lvl = skillComp.GetEasySkill(skillType);

            if (lvl != false)
                return false;

            int exp = skillComp.GetSkillExp(skillType);

            exp += experienceAmount;

            if (exp >= _EXPERIENCETONEWLVL)
            {
                SetEasySkill(skillComp, skillType);
                SetSkillExp(skillComp, skillType, 0);
                if (player== null)
                    return true;
                _audio.PlayGlobal("/Audio/Vanilla/SkillSystem/levelup.ogg", player, audioParams: AudioParams.Default.WithVolume(-6f));
                RaiseNetworkEvent(new UpdateCharacterSkillsRequestEvent(), Filter.SinglePlayer(player));
                return true;
            }
            SetSkillExp(skillComp, skillType, exp);
            if (player != null) RaiseNetworkEvent(new UpdateCharacterSkillsRequestEvent(), Filter.SinglePlayer(player));
            return false;
        }
        else
        {
            SkillLevel? level = skillComp.GetSkillLevel(skillType);
            int exp = skillComp.GetSkillExp(skillType);

            if (level == null || level >= SkillLevel.Expert) return false;

            exp += experienceAmount;

            if (exp >= _EXPERIENCETONEWLVL)
            {
                SetSkillLevel(skillComp, skillType, level.Value + 1);
                SetSkillExp(skillComp, skillType, exp - _EXPERIENCETONEWLVL);

                if (player == null)
                    return true;

                _audio.PlayGlobal("/Audio/Vanilla/SkillSystem/levelup.ogg", player, audioParams: AudioParams.Default.WithVolume(-6f));
                RaiseNetworkEvent(new UpdateCharacterSkillsRequestEvent(), Filter.SinglePlayer(player));

                return true;
            }
            SetSkillExp(skillComp, skillType, exp);
            if (player != null) RaiseNetworkEvent(new UpdateCharacterSkillsRequestEvent(), Filter.SinglePlayer(player));
            return false;
        }

    }
    
    private void SetEasySkill(SkillComponent skillComp, skillType skill)
    {
        switch (skill)
        {
            case skillType.Piloting:
                skillComp.Piloting = true;
                break;
            case skillType.Botany:
                skillComp.Botany = true;
                break;
            case skillType.MusInstruments:
                skillComp.MusInstruments = true;
                break;
            case skillType.Bureaucracy:
                skillComp.Bureaucracy = true;
                break;
            case skillType.Thief:
                skillComp.Thief = true;
                break;
            case skillType.Stealth:
                skillComp.Stealth = true;
                break;
            default:
                break;
        }
        skillComp.Dirty();
    }

    private void SetSkillLevel(SkillComponent skillComp, skillType skill, SkillLevel level)
    {
        switch (skill)
        {
            case skillType.RangeWeapon:
                skillComp.RangeWeaponLevel = level;
                break;
            case skillType.MeleeWeapon:
                skillComp.MeleeWeaponLevel = level;
                break;
            case skillType.Medicine:
                skillComp.MedicineLevel = level;
                break;
            case skillType.Chemistry:
                skillComp.ChemistryLevel = level;
                break;
            case skillType.Engineering:
                skillComp.EngineeringLevel = level;
                break;
            case skillType.Building:
                skillComp.BuildingLevel = level;
                break;
            case skillType.Research:
                skillComp.ResearchLevel = level;
                break;
            case skillType.Instrumentation:
                skillComp.InstrumentationLevel = level;
                break;
            default:
                break;
        }
        skillComp.Dirty();
    }

    private void SetSkillExp(SkillComponent skillComp, skillType skill, int exp)
    {
        if (exp < 0) return;

        switch (skill)
        {
            case skillType.Piloting:
                skillComp.PilotingExp = exp;
                break;
            case skillType.RangeWeapon:
                skillComp.RangeWeaponExp = exp;
                break;
            case skillType.MeleeWeapon:
                skillComp.MeleeWeaponExp = exp;
                break;
            case skillType.Medicine:
                skillComp.MedicineExp = exp;
                break;
            case skillType.Chemistry:
                skillComp.ChemistryExp = exp;
                break;
            case skillType.Engineering:
                skillComp.EngineeringExp = exp;
                break;
            case skillType.Building:
                skillComp.BuildingExp = exp;
                break;
            case skillType.Research:
                skillComp.ResearchExp = exp;
                break;
            case skillType.Instrumentation:
                skillComp.InstrumentationExp = exp;
                break;
            case skillType.Botany:
                skillComp.BotanyExp = exp;
                break;
            case skillType.MusInstruments:
                skillComp.MusInstrumentsExp = exp;
                break;
            case skillType.Bureaucracy:
                skillComp.BureaucracyExp = exp;
                break;
            case skillType.Thief:
                skillComp.ThiefExp = exp;
                break;
            case skillType.Stealth:
                skillComp.StealthExp = exp;
                break;
            default:
                break;
        }
        skillComp.Dirty();
    }
}
