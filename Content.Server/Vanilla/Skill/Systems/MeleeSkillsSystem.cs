using Content.Shared.Weapons.Melee;
using Content.Shared.Vanilla.Skill;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.FixedPoint;
using Content.Shared.Damage;
using System.Linq;

namespace Content.Server.Vanilla.Skill;

public sealed class MeleeSkillSystem : EntitySystem
{
    private ISawmill _sawmill = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MeleeWeaponComponent, GetMeleeDamageEvent>(OnMeleeDamage);
               _sawmill = Logger.GetSawmill("MELEE");
    }

private void OnMeleeDamage(Entity<MeleeWeaponComponent> entity, ref GetMeleeDamageEvent args)
{
    // Проверяем, есть ли у игрока компонент скилла
    if (!TryComp<SkillComponent>(args.User, out var userskill) || userskill.MeleeWeaponLevel==2)
        return;

    // Получаем базовый урон
    var baseDamage = args.Damage.GetTotal();

    // Если суммарный урон равен 0, прекращаем выполнение
    if (baseDamage == 0)
        return;

    // Уровень скилла игрока
    var lvl = userskill.MeleeWeaponLevel;

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