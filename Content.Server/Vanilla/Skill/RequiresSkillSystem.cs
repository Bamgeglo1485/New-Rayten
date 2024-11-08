using Content.Shared.UserInterface;
using Content.Shared.Interaction;
using Content.Shared.Vanilla.Skill;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Interaction.Events;
using Content.Server.Popups;
using Content.Shared.Chemistry.Components;
using Content.Shared.Hands;
using ActivatableUISystem = Content.Shared.UserInterface.ActivatableUISystem;

namespace Content.Server.Vanilla.Skill;

public sealed class RequiresSkillSystem : SharedRequiresSkillSystem
{
    [Dependency] private readonly ActivatableUISystem _activatableUI = default!;
    [Dependency] private readonly PopupSystem _popupSystem = default!;

    protected override void OnActivate(EntityUid uid, RequiresSkillComponent component, ref ActivatableUIOpenAttemptEvent args)
    {
        if (args.Cancelled || HasRequiredSkills(args.User, component, true))
            return;
        args.Cancel();
    }
    protected override void OnItemSlotInsertAttempt(EntityUid uid, RequiresSkillComponent component, ref ItemSlotInsertAttemptEvent args)
    {
        if (uid != args.SlotEntity || args.Cancelled || args.User == null)
            return;

        if (HasRequiredSkills(args.User.Value, component, true))
            return;
            
        args.Cancelled = true;
    }
    protected override void OnItemSlotEjectAttempt(EntityUid uid, RequiresSkillComponent component, ref ItemSlotEjectAttemptEvent args)
    {
        if (uid != args.SlotEntity || args.Cancelled || args.User == null)
            return;

        if (HasRequiredSkills(args.User.Value, component, true))
            return;
            
        args.Cancelled = true;
    }
    protected override void OnHandPickUp(EntityUid uid, RequiresSkillComponent component, ref GotEquippedHandEvent args)
    {
        if (args.Handled || args.User == null)
            return;
        solveskilldiff(args.User, component);
            
        args.Handled = true;
    }
    protected override void OnHandDrop(EntityUid uid, RequiresSkillComponent component, ref GotUnequippedHandEvent args)
    {
        if (args.Handled || args.User == null)
            return;
        //исследования
        component.SkillDiffResearchLevel = 0;
        component.SkillDiffMedicineLevel = 0;
        args.Handled = true;
    }

    public void solveskilldiff(EntityUid user, RequiresSkillComponent component)
    {
        if (!EntityManager.TryGetComponent<SkillComponent>(user, out var skillComp))
            skillComp = EnsureComp<SkillComponent>(user);
        //исследования
        component.SkillDiffResearchLevel = component.RequiresResearchLevel - skillComp.ResearchLevel;
        //Медицина
        component.SkillDiffMedicineLevel = component.RequiresMedicineLevel - skillComp.MedicineLevel;
    }

    public bool HasRequiredSkills(EntityUid user, RequiresSkillComponent component, bool popup)
    {
        // Проверка уровня химии
        if (!HasSkillLevel(user, component.RequiresChemistryLevel, skillComponent => skillComponent.ChemistryLevel)){
            if(popup)
                _popupSystem.PopupEntity(Loc.GetString("Skill-issue-message-chemistry-unskilled", ("lvl", component.RequiresChemistryLevel)), user, user);
            return false;
        }
        // Проверка уровня медицины
        if (!HasSkillLevel(user, component.RequiresMedicineLevel, skillComponent => skillComponent.MedicineLevel)){
            if(popup)
            _popupSystem.PopupEntity(Loc.GetString("Skill-issue-message-medicine-unskilled", ("lvl", component.RequiresMedicineLevel)), user, user);
            return false;
        }
        // Проверка уровня пилотирования
        if (!HasSkillLevel(user, component.RequiresPilotingLevel, skillComponent => skillComponent.PilotingLevel)){
            if(popup)
            _popupSystem.PopupEntity(Loc.GetString("Skill-issue-message-piloting-unskilled", ("lvl", component.RequiresPilotingLevel)), user, user);
            return false;
        }
        // Проверка уровня исследования
        if (!HasSkillLevel(user, component.RequiresResearchLevel, skillComponent => skillComponent.ResearchLevel)){
            if(popup)
            _popupSystem.PopupEntity(Loc.GetString("Skill-issue-message-research-unskilled", ("lvl", component.RequiresResearchLevel)), user, user);
            return false;
        }
        return true;
    }
    public bool HasRequiredSkillsForCraft(EntityUid user, RequiresSkillComponent component, bool popup)
    {
        // Проверка уровня Приборостроения
        if (!HasSkillLevel(user, component.RequiresInstrumentationLevel, skillComponent => skillComponent.InstrumentationLevel)){
            if(popup)
            _popupSystem.PopupEntity(Loc.GetString("Skill-issue-message-instrumentation-unskilled", ("lvl", component.RequiresInstrumentationLevel)), user, user);
            return false;
        }
        //  // Проверка уровня Строительства
        // if (!HasSkillLevel(user, component.RequiresInstrumentationLevel, skillComponent => skillComponent.InstrumentationLevel)){
        //     if(popup)
        //     _popupSystem.PopupEntity(Loc.GetString("Skill-issue-message-building-unskilled", ("lvl", component.RequiresInstrumentationLevel)), user, user);
        //     return false;
        // }
        return true;
    }
    public bool HasSkillLevel(EntityUid user, int requiredLevel, Func<SkillComponent, int> skillSelector)
    {
        if (TryComp<SkillComponent>(user, out var skillComponent) && skillSelector(skillComponent) >= requiredLevel)
            return true;
        return false;
    }
}

