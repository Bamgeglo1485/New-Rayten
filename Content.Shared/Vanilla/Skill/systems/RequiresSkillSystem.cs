using Content.Shared.UserInterface;
using Content.Shared.Interaction;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Interaction.Events;
using Content.Shared.Hands;
using Content.Shared.Popups;

namespace Content.Shared.Vanilla.Skill;

public abstract class SharedRequiresSkillSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RequiresSkillComponent, ActivatableUIOpenAttemptEvent>(OnActivate);//Открытие интерфейса
        SubscribeLocalEvent<RequiresSkillComponent, ItemSlotInsertAttemptEvent>(OnItemSlotInsertAttempt); //попытка вставить что-то
        SubscribeLocalEvent<RequiresSkillComponent, ItemSlotEjectAttemptEvent>(OnItemSlotEjectAttempt); //попытка вытащить что-то
        SubscribeLocalEvent<RequiresSkillToActivateInWorldComponent, ActivateInWorldEvent >(OnSkillCheckToActivateInWorld);
    }
    public bool HasRequiredSkills(EntityUid user, RequiresSkillComponent component)
    {

        if(!TryComp<SkillComponent>(user, out var skill))
            skill = EnsureComp<SkillComponent>(user);

        // Проверка уровня химии
        if (!HasSkillLevel(user, component.RequiresChemistryLevel, skillComponent => skillComponent.ChemistryLevel)){
            return false;
        }
        // Проверка уровня медицины
        if (!HasSkillLevel(user, component.RequiresMedicineLevel, skillComponent => skillComponent.MedicineLevel)){
            return false;
        }
        // Проверка уровня пилотирования
        if (!HasSkillLevel(user, component.RequiresPilotingLevel, skillComponent => skillComponent.PilotingLevel)){
            return false;
        }
        // Проверка уровня исследования
        if (!HasSkillLevel(user, component.RequiresResearchLevel, skillComponent => skillComponent.ResearchLevel)){
            return false;
        }
        // Проверка уровня Инженерии
        if (!HasSkillLevel(user, component.RequiresEngineeringLevel, skillComponent => skillComponent.EngineeringLevel)){
            return false;
        }
        return true;
    }
    public bool HasRequiredSkills(EntityUid user, RequiresSkillComponent component, bool or = true)
    {
        // Проверка уровня химии
        if (component.RequiresChemistryLevel!=0 && HasSkillLevel(user, component.RequiresChemistryLevel, skillComponent => skillComponent.ChemistryLevel)){
            return true;
        }
        // Проверка уровня медицины
        if (component.RequiresMedicineLevel!=0 && HasSkillLevel(user, component.RequiresMedicineLevel, skillComponent => skillComponent.MedicineLevel)){
            return true;
        }
        // Проверка уровня пилотирования
        if (component.RequiresPilotingLevel!=0 && HasSkillLevel(user, component.RequiresPilotingLevel, skillComponent => skillComponent.PilotingLevel)){
            return true;
        }
        // Проверка уровня исследования
        if (component.RequiresResearchLevel!=0 && HasSkillLevel(user, component.RequiresResearchLevel, skillComponent => skillComponent.ResearchLevel)){
            return true;
        }
        // Проверка уровня Инженерии
        if (component.RequiresEngineeringLevel!=0 && HasSkillLevel(user, component.RequiresEngineeringLevel, skillComponent => skillComponent.EngineeringLevel)){
            return true;
        }
        return false;
    }

    public bool HasRequiredSkillsForCraft(EntityUid user, RequiresSkillComponent component, bool popup = false)
    {

        // Проверка уровня Приборостроения
        if (!HasSkillLevel(user, component.RequiresInstrumentationLevel, skillComponent => skillComponent.InstrumentationLevel)){
            if(popup)
                _popup.PopupClient(Loc.GetString("Skill-issue-message-instrumentation-unskilled", ("lvl", component.RequiresInstrumentationLevel)), user, user);
            return false;
        }

        // Проверка уровня Строительства
        if (!HasSkillLevel(user, component.RequiresBuildingLevel, skillComponent => skillComponent.BuildingLevel)){
            if(popup)
                _popup.PopupClient(Loc.GetString("Skill-issue-message-building-unskilled", ("lvl", component.RequiresBuildingLevel)), user, user);
            return false;
        }

        return true;
    }
    public bool HasSkillLevel(EntityUid user, SkillLevel requiredLevel, Func<SkillComponent, SkillLevel> skillSelector)
    {
        if (TryComp<SkillComponent>(user, out var skillComponent) && skillSelector(skillComponent) >= requiredLevel)
            return true;
        return false;
    }

    protected abstract void OnActivate(EntityUid uid, RequiresSkillComponent component, ref ActivatableUIOpenAttemptEvent args);//Открытие интерфейса консоли
    protected abstract void OnItemSlotInsertAttempt(EntityUid uid, RequiresSkillComponent component, ref ItemSlotInsertAttemptEvent args);//попытка вставить что-то
    protected abstract void OnItemSlotEjectAttempt(EntityUid uid, RequiresSkillComponent component, ref ItemSlotEjectAttemptEvent args);//попытка вытащить что-то
    protected abstract void OnSkillCheckToActivateInWorld(EntityUid uid, RequiresSkillToActivateInWorldComponent component, ref ActivateInWorldEvent args);//если есть компонент то обрабатываем активацию
}