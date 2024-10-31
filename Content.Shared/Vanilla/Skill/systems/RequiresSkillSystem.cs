using Content.Shared.UserInterface;
using Content.Shared.Interaction;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Interaction.Events;
using Content.Shared.Chemistry.Components;
namespace Content.Shared.Vanilla.Skill;

public abstract class SharedRequiresSkillSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RequiresSkillComponent, ActivatableUIOpenAttemptEvent>(OnActivate);//Открытие интерфейса
        SubscribeLocalEvent<RequiresSkillComponent, ActivateInWorldEvent>(OnActivateInWorld);//Взаимодействие (любое)
        SubscribeLocalEvent<RequiresSkillComponent, InjectorDoAfterEvent>(OnInjectorDoAfter);//Вкалываение шприца
        SubscribeLocalEvent<RequiresSkillComponent, ItemSlotInsertAttemptEvent>(OnItemSlotInsertAttempt); //попытка вставить что-то во что-то через пкм
        SubscribeLocalEvent<RequiresSkillComponent, ItemSlotEjectAttemptEvent>(OnItemSlotEjectAttempt); //попытка вытащить что-то из чего-то
    }

    protected abstract void OnActivate(EntityUid uid, RequiresSkillComponent component, ref ActivatableUIOpenAttemptEvent args);
    protected abstract void OnActivateInWorld(EntityUid uid, RequiresSkillComponent component, ref ActivateInWorldEvent args);
    protected abstract void OnInjectorDoAfter(EntityUid uid, RequiresSkillComponent component, ref InjectorDoAfterEvent args);
    protected abstract void OnItemSlotInsertAttempt(EntityUid uid, RequiresSkillComponent component, ref ItemSlotInsertAttemptEvent args);
    protected abstract void OnItemSlotEjectAttempt(EntityUid uid, RequiresSkillComponent component, ref ItemSlotEjectAttemptEvent args);
}
