using Content.Shared.Vanilla.Skill;
using Content.Shared.Mobs.Components;
using Content.Shared.Inventory;
using Content.Shared.Weapons.Melee;
using Content.Shared.FixedPoint;
using Content.Shared.Damage;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server.Vanilla.Skill;

public class SkillTrainerSystem : EntitySystem
{
    const int _EXPERIENCETONEWLVL = 600;
    const int _EXPERIENCEFROMSKILLPOINT = 600;
    public override void Initialize()
    {
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
        if (skillComp.GetSkillLevel(msg.skill) == null && skillComp.GetEasySkill(msg.skill) == null)
            return;

        //проверка основных скилов
        if (skillComp.GetSkillLevel(msg.skill) != null && skillComp.GetSkillLevel(msg.skill) >= SkillLevel.Expert)
            return;

        //проверка легких скилов
        if (skillComp.GetEasySkill(msg.skill) != null && skillComp.GetEasySkill(msg.skill) == true)
            return;

        // Уменьшаем очки навыков
        skillComp.SkillPoints--;
        skillComp.Dirty();

        // Добавляем опыт
        AddExperience(skillComp, msg.skill, _EXPERIENCEFROMSKILLPOINT, multiplyed: false);
    }
    public bool AddExperience(SkillComponent skillComp, skillType skillType, int experienceAmount, bool multiplyed = true)
    {
        if (SkillComponent.IsEasySkill(skillType))
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
                return true;
            }
            SetSkillExp(skillComp, skillType, exp);
            return false;
        }
        else
        {
            SkillLevel? level = skillComp.GetSkillLevel(skillType);
            int exp = skillComp.GetSkillExp(skillType);

            if (level == null || level >= SkillLevel.Expert)
                return false;

            exp += experienceAmount;

            if (exp >= _EXPERIENCETONEWLVL)
            {
                SetSkillLevel(skillComp, skillType, level.Value + 1);
                SetSkillExp(skillComp, skillType, exp - _EXPERIENCETONEWLVL);
                return true;
            }
            SetSkillExp(skillComp, skillType, exp);
            return false;
        }

    }
    public void SetEasySkill(SkillComponent skillComp, skillType skill)
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
            case skillType.Atmosphere:
                skillComp.Atmosphere = true;
                break;
            case skillType.Research:
                skillComp.Research = true;
                break;
            default:
                break;
        }
        skillComp.Dirty();
    }

    public void SetSkillLevel(SkillComponent skillComp, skillType skill, SkillLevel level)
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
            case skillType.Crime:
                skillComp.CrimeLevel = level;
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
            case skillType.Research:
                skillComp.ResearchExp = exp;
                break;
            case skillType.Crime:
                skillComp.CrimeExp = exp;
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
            case skillType.Atmosphere:
                skillComp.AtmosphereExp = exp;
                break;
            default:
                break;
        }
        skillComp.Dirty();
    }
}
