using Content.Shared.Vanilla.Skill;
using Robust.Shared.GameObjects;

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
            skillComp.Dirty();
            RaiseNetworkEvent(new UpdateCharacterSkillsRequestEvent(GetNetEntity(uid)));
            EntityManager.RemoveComponent<AddSkillPointsComponent>(uid);
        }
    }
}
