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

namespace Content.Shared.Vanilla.Skill;

public class SharedSkillTrainerSystem : EntitySystem
{
    const int _EXPERIENCETONEWLVL = 600;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MeleeWeaponComponent, MeleeHitEvent>(OnMeleeHitTraining);
        SubscribeLocalEvent<MeleeTrainerComponent, MeleeHitEvent>(OnBoxTrainHit);
        SubscribeLocalEvent<MeleeTrainerComponent, InventoryRelayedEvent<MeleeHitEvent>>(
            (e, c, ev) => OnBoxTrainHit(e, c, ev.Args));
    }
    private void OnMeleeHitTraining(EntityUid uid, MeleeWeaponComponent component, MeleeHitEvent args)
    {
        if (!args.IsHit)
            return;

        if (!TryComp<SkillComponent>(args.User, out var skillcomp))
            return;
        // Считаем урон с учётом штрафа
        var totalDamage = args.BaseDamage + args.BonusDamage;
        int experience = (int) totalDamage.GetTotal().Float();

        // Перебираем всех, кого ударили
        foreach (var target in args.HitEntities)
        {
            if (target == args.User)
                continue;

            if (!TryComp<MobStateComponent>(target, out var targetmobstate))
                continue;

            if (targetmobstate.CurrentState != MobState.Alive)
                continue;

            AddExperience(skillcomp, skillType.MeleeWeapon, experience);
            return;
        }
    }
    private void OnBoxTrainHit(EntityUid uid, MeleeTrainerComponent component, MeleeHitEvent args)
    {
        // Проверяем, что удар действительно произошел
        if (!args.IsHit)
            return;

        // Проверяем, является ли атакующий игроком
        if (!HasComp<ActorComponent>(args.User))
            return;

        // Проверяем, есть ли у атакующего компонент SkillComponent
        if (!TryComp<SkillComponent>(args.User, out var skillCompAttacker))
            skillCompAttacker = EnsureComp<SkillComponent>(args.User);

        // Перебираем всех сущностей, которые были атакованы
        foreach (var target in args.HitEntities)
        {
            //Проверяем что чел не пиздит сам себя
            if (target == args.User)
                continue;

            //Проверяем что цель живой игрок
            if (!HasComp<ActorComponent>(target))
                continue;

            // Начисляем опыт за атаку атакующему
            AddExperience(skillCompAttacker, component.SkillType, component.ExpPerHit);
        }
    }
    public bool AddExperience(SkillComponent skillComp, skillType skillType, int experienceAmount, bool multiplyed = true)
    {
        if (multiplyed)
        {
            if ((int)skillComp.ResearchLevel == 3)
                experienceAmount *= 2;
        }
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
            case skillType.Building:
                skillComp.BuildingLevel = level;
                break;
            case skillType.Research:
                skillComp.ResearchLevel = level;
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
            case skillType.Building:
                skillComp.BuildingExp = exp;
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
