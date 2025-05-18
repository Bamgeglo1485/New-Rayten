using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.FixedPoint;
using Content.Shared.Damage;
using Content.Shared.Inventory;
using Content.Shared.Damage.Events;
using Robust.Shared.Player;

using System.Linq;

namespace Content.Shared.Vanilla.Skill;

public sealed class SharedMeleeSkillSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MeleeWeaponComponent, GetMeleeDamageEvent>(OnMeleeDamage);
    }
    private void OnMeleeDamage(Entity<MeleeWeaponComponent> entity, ref GetMeleeDamageEvent args)
    {
        // Проверяем, есть ли у игрока компонент скилла
        if (!TryComp<SkillComponent>(args.User, out var userskill))
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

}
