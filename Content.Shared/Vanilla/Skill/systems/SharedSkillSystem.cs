using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Chemistry;
using Content.Shared.UserInterface;
using Content.Shared.Interaction;
using Content.Shared.Containers.ItemSlots;

namespace Content.Shared.Vanilla.Skill;

public sealed partial class SharedSkillSystem : EntitySystem
{
    const int _EXPERIENCEFROMSKILLPOINT = 600;
    const int _EXPERIENCETONEWLVL = 600;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeAllEvent<UseSkillPointEvent>(UseSkillPoint);

        SubscribeLocalEvent<MeleeWeaponComponent, GetMeleeDamageEvent>(OnMeleeDamage);
        SubscribeLocalEvent<SkillComponent, SolutionScanEvent>(OnChemScan, after: [typeof(SolutionScannerSystem)]);

        SubscribeLocalEvent<RequiresSkillComponent, ActivatableUIOpenAttemptEvent>(OnActivate);//Открытие интерфейса
        SubscribeLocalEvent<RequiresSkillComponent, ItemSlotInsertAttemptEvent>(OnItemSlotInsertAttempt); //попытка вставить что-то
        SubscribeLocalEvent<RequiresSkillComponent, ItemSlotEjectAttemptEvent>(OnItemSlotEjectAttempt); //попытка вытащить что-то
        SubscribeLocalEvent<RequiresSkillToActivateInWorldComponent, ActivateInWorldEvent>(OnSkillCheckToActivateInWorld);
    }

    private void UseSkillPoint(UseSkillPointEvent msg, EntitySessionEventArgs args)
    {
        if (!args.SenderSession.AttachedEntity.HasValue)
            return;

        var entity = args.SenderSession.AttachedEntity.Value;

        if (!TryComp<SkillComponent>(entity, out var skillComp) || skillComp.SkillPoints < 1)
            return;

        if (AddExperience((entity, skillComp), msg.skill, _EXPERIENCEFROMSKILLPOINT))
        {
            skillComp.SkillPoints--;
            Dirty(entity, skillComp);
        }
    }

    public bool AddExperience(Entity<SkillComponent> ent, SkillType skillType, int experienceAmount)
    {
        if (experienceAmount <= 0)
            return false;

        // Получаем накопленный опыт
        var exp = ent.Comp.SkillExps.GetValueOrDefault(skillType);
        exp += experienceAmount;

        if (IsEasySkill(skillType))
        {
            // Навык уже изучен
            if (ent.Comp.EasySkills.Contains(skillType))
                return false;

            if (exp >= _EXPERIENCETONEWLVL)
            {
                ent.Comp.SkillExps[skillType] = exp - _EXPERIENCETONEWLVL;
                ent.Comp.EasySkills.Add(skillType);
            }
            else
            {
                ent.Comp.SkillExps[skillType] = exp;
            }
        }
        else
        {
            var lvl = ent.Comp.BasicSkills.GetValueOrDefault(skillType, SkillLevel.None);
            // Навык уже изучен
            if (lvl == SkillLevel.Expert)
                return false;

            if (exp >= _EXPERIENCETONEWLVL)
            {
                ent.Comp.SkillExps[skillType] = exp - _EXPERIENCETONEWLVL;
                ent.Comp.BasicSkills[skillType] = lvl + 1;
            }
            else
            {
                ent.Comp.SkillExps[skillType] = exp;
            }
        }

        Dirty(ent);
        return true;
    }

    #region help
    public bool TryGetSkill(EntityUid uid, SkillType skill, out bool hasEasySkill, out SkillLevel level, SkillComponent? component = null)
    {
        hasEasySkill = false;
        level = SkillLevel.None;

        if (!Resolve(uid, ref component))
            return false;

        if (IsEasySkill(skill))
        {
            hasEasySkill = component.EasySkills.Contains(skill);
            return true;
        }

        level = component.BasicSkills.GetValueOrDefault(skill, SkillLevel.None);
        return true;
    }
    /// <summary>
    /// Возвращает true, если изученный навык является лёгким
    /// Возвращает false, если изученный навык НЕ является лёгким
    /// </summary>
    public static bool IsEasySkill(SkillType skill)
    {
        return skill switch
        {
            SkillType.Piloting => true,
            SkillType.MusInstruments => true,
            SkillType.Botany => true,
            SkillType.Bureaucracy => true,
            SkillType.Research => true,
            _ => false
        };
    }
    #endregion
}