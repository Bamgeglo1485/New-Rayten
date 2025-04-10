using Content.Server.SkillTrainer;
using Content.Shared.Weapons.Melee;
using Content.Shared.Vanilla.Skill;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.FixedPoint;
using Content.Shared.Damage;
using Content.Shared.Inventory;
using Content.Shared.Damage.Events;
using Robust.Shared.Player;

using System.Linq;

namespace Content.Server.Vanilla.Skill;

public sealed class MeleeSkillSystem : EntitySystem
{

    [Dependency] private readonly ServerSkillTrainerSystem _skillTrainerSystem = default!;
    
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MeleeWeaponComponent, GetMeleeDamageEvent>(OnMeleeDamage);
        // SubscribeLocalEvent<MeleeWeaponComponent, StaminaMeleeHitEvent>(OnStaminaMeleeHit); TODO: client-side too
         
        SubscribeLocalEvent<MeleeTrainerComponent, MeleeHitEvent>(OnMeleeTrainerHit);
        SubscribeLocalEvent<MeleeTrainerComponent, InventoryRelayedEvent<MeleeHitEvent>>(
            (e, c, ev) => OnMeleeTrainerHit(e, c, ev.Args));
    }


    private void OnMeleeTrainerHit(EntityUid uid, MeleeTrainerComponent component, MeleeHitEvent args)
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
            if(!HasComp<ActorComponent>(target))
                continue;

            // Начисляем опыт за атаку атакующему
            _skillTrainerSystem.AddExperience(skillCompAttacker, component.SkillType, component.ExpPerHit);

            // начисляем опыт за атаку атакуемому
            if (!TryComp<SkillComponent>(target, out var skillCompAttacked))
                skillCompAttacked = EnsureComp<SkillComponent>(target);

            _skillTrainerSystem.AddExperience(skillCompAttacked, component.SkillType, component.ExpPerHit);
        }
    }


    private void OnMeleeDamage(Entity<MeleeWeaponComponent> entity, ref GetMeleeDamageEvent args)
    {
        // Проверяем, есть ли у игрока компонент скилла
        if (!TryComp<SkillComponent>(args.User, out var userskill))
            return;

        // Получаем базовый урон
        // Если суммарный урон равен 0, прекращаем выполнение
        var baseDamage = args.Damage.GetTotal();
        if (baseDamage == 0)
            return;

        // Определяем множитель в зависимости от уровня
        FixedPoint2 damageMultiplier = userskill.MeleeWeaponLevel switch
        {
            SkillLevel.None => 0.5f,         // 50% 
            SkillLevel.Basic => 0.7f,        // 70%
            SkillLevel.Advanced => 0.9f,     // 90%
            SkillLevel.Expert => 1.1f,       // 110% 
            _ => 1f
        };

        // Умножаем весь урон на множитель
        args.Damage *= damageMultiplier;
    }
    private void OnStaminaMeleeHit(Entity<MeleeWeaponComponent> entity, ref StaminaMeleeHitEvent args)
    {
        // Проверяем, есть ли у игрока компонент скилла
        if (!TryComp<SkillComponent>(args.User, out var userskill))
            return;

        args.Multiplier *= userskill.MeleeWeaponLevel switch
        {
            SkillLevel.None => 0.5f,         // 50% 
            SkillLevel.Basic => 0.7f,        // 70%
            SkillLevel.Advanced => 0.9f,     // 90%
            SkillLevel.Expert => 1.1f,       // 110% 
            _ => 1f
        };
    }

}