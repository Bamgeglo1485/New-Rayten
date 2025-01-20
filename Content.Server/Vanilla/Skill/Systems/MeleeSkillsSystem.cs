using Content.Server.SkillTrainer;
using Content.Shared.Weapons.Melee;
using Content.Shared.Vanilla.Skill;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.FixedPoint;
using Content.Shared.Damage;
using Content.Shared.Inventory;
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
        if (!TryComp<ActorComponent>(args.User, out var actorAttacker))
            return;

        // Проверяем, есть ли у атакующего компонент SkillComponent
        if (!TryComp<SkillComponent>(args.User, out var skillCompAttacker))
            skillCompAttacker = EnsureComp<SkillComponent>(args.User);

        // Перебираем всех сущностей, которые были атакованы
        foreach (var target in args.HitEntities)
        {
            // Проверяем, является ли цель игроком
            if (!TryComp<ActorComponent>(target, out var actorattacked))
                continue;

            //Проверяем что чел не пиздит сам себя
            if (target == args.User)
                continue;

            // Начисляем опыт за атаку атакующему
            _skillTrainerSystem.AddExperience(skillCompAttacker, component.SkillType, component.ExpPerHit);
            RaiseNetworkEvent(new UpdateCharacterSkillsRequestEvent(), Filter.SinglePlayer(actorAttacker.PlayerSession));

            // начисляем опыт за атаку атакуемому
            if (!TryComp<SkillComponent>(target, out var skillCompAttacked))
                skillCompAttacked = EnsureComp<SkillComponent>(target);

            _skillTrainerSystem.AddExperience(skillCompAttacked, component.SkillType, component.ExpPerHit);
            RaiseNetworkEvent(new UpdateCharacterSkillsRequestEvent(), Filter.SinglePlayer(actorattacked.PlayerSession));
            return;
        }
    }


    private void OnMeleeDamage(Entity<MeleeWeaponComponent> entity, ref GetMeleeDamageEvent args)
    {
        // Проверяем, есть ли у игрока компонент скилла
        if (!TryComp<SkillComponent>(args.User, out var userskill) || userskill.MeleeWeaponLevel==SkillLevel.Advanced)
            return;

        // Получаем базовый урон
        var baseDamage = args.Damage.GetTotal();

        // Если суммарный урон равен 0, прекращаем выполнение
        if (baseDamage == 0)
            return;

        // Уровень скилла игрока
        int lvl = (int)userskill.MeleeWeaponLevel;

        // Расчёт изменения урона
        var reductionFactor = (baseDamage * FixedPoint2.New(1) / 5) * (2 - lvl);

        // Фактическая сумма изменений
        FixedPoint2 actualChange = 0;

        // Проходим по каждому типу урона
        foreach (var key in args.Damage.DamageDict.Keys.ToList())
        {
            var currentDamage = args.Damage.DamageDict[key];
            var damagePortion = currentDamage / baseDamage;
            var changeForType = reductionFactor * damagePortion;

            // Вычисляем новый урон
            var newDamage = currentDamage - changeForType;

            // Обновляем урон
            args.Damage.DamageDict[key] = newDamage;

            // Добавляем в фактическую сумму изменений
            actualChange += changeForType;
        }

        // Проверяем разницу между требуемым и фактическим изменением
        var adjustment = reductionFactor - actualChange;

        // Если есть разница, равномерно распределяем её
        if (adjustment != 0)
        {
            var damageTypes = args.Damage.DamageDict.Keys.ToList();
            foreach (var key in damageTypes)
            {
                args.Damage.DamageDict[key] -= adjustment / damageTypes.Count;
            }
        }
    }
}