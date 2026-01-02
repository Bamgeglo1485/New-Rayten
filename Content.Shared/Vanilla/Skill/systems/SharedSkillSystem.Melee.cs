using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.FixedPoint;
namespace Content.Shared.Vanilla.Skill;

public sealed partial class SharedSkillSystem : EntitySystem
{
    private void OnMeleeDamage(Entity<MeleeWeaponComponent> entity, ref GetMeleeDamageEvent args)
    {
        if (!TryGetSkill(args.User, SkillType.Weapon, out _, out var skillLevel))
            return;

        // Определяем множитель в зависимости от уровня
        FixedPoint2 damageMultiplier = skillLevel switch
        {
            SkillLevel.None => 0.5f,
            SkillLevel.Basic => 0.75f,
            SkillLevel.Advanced => 1.0f,
            SkillLevel.Expert => 1.0f,
            _ => 1f
        };

        // Умножаем весь урон на множитель
        args.Damage *= damageMultiplier;
    }
}