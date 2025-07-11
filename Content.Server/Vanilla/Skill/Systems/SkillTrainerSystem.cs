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

public class SkillTrainerSystem : SharedSkillTrainerSystem
{

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

}
