using Content.Shared.Vanilla.Skill;
using Robust.Shared.GameObjects;
using Robust.Shared.Player;

namespace Content.Server.Vanilla.Skill
{
    public sealed class SkillPointsSetterSystem : EntitySystem
    {

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<AddSkillPointsComponent, MapInitEvent>(OnSetterComponentInitialized);
        }

        private void OnSetterComponentInitialized(EntityUid uid, AddSkillPointsComponent setterComp, MapInitEvent args)
        {
            // Проверяем наличие SkillPointsComponent
            if (!EntityManager.TryGetComponent<SkillComponent>(uid, out var skillComp))
                skillComp = EnsureComp<SkillComponent>(uid);

            // Добавляем значения
            skillComp.SkillPoints += setterComp.Points;
            Dirty(uid, skillComp);

            EntityManager.RemoveComponent<AddSkillPointsComponent>(uid);
        }
    }
}
