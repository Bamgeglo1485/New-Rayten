using Content.Shared.UserInterface;
using Content.Shared.Interaction;
using Content.Shared.Vanilla.Skill;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Interaction.Events;
using Content.Server.Popups;
using Content.Shared.Chemistry.Components;
using ActivatableUISystem = Content.Shared.UserInterface.ActivatableUISystem;

namespace Content.Server.Vanilla.Skill;

public sealed class RequiresSkillSystem : SharedRequiresSkillSystem
{
    [Dependency] private readonly ActivatableUISystem _activatableUI = default!;
    [Dependency] private readonly PopupSystem _popupSystem = default!;

    protected override void OnActivate(EntityUid uid, RequiresSkillComponent component, ref ActivatableUIOpenAttemptEvent args)
    {
        if (args.Cancelled || HasRequiredSkills(args.User, component))
            return;
        args.Cancel();
    }
    protected override void OnActivateInWorld(EntityUid uid, RequiresSkillComponent component, ref ActivateInWorldEvent args)
    {
        if (args.Handled || HasRequiredSkills(args.User, component))
            return;
        args.Handled = true;
    }
    protected override void OnInjectorDoAfter(EntityUid uid, RequiresSkillComponent component, ref InjectorDoAfterEvent args)
    {
        if (args.Handled || HasRequiredSkills(args.User, component))
            return;
        args.Handled = true;
    }
    protected override void OnItemSlotInsertAttempt(EntityUid uid, RequiresSkillComponent component, ref ItemSlotInsertAttemptEvent args)
    {
        if (args.Cancelled || args.User == null)
            return;

        if (HasRequiredSkills(args.User.Value, component))
            return;
            
        args.Cancelled = true;
    }
    protected override void OnItemSlotEjectAttempt(EntityUid uid, RequiresSkillComponent component, ref ItemSlotEjectAttemptEvent args)
    {
        if (args.Cancelled || args.User == null)
            return;

        if (HasRequiredSkills(args.User.Value, component))
            return;
            
        args.Cancelled = true;
    }


    public bool HasRequiredSkills(EntityUid user, RequiresSkillComponent component)
    {
        // Проверка уровня химии
        if (!HasSkillLevel(user, component.RequiresChemistryLevel, skillComponent => skillComponent.ChemistryLevel)){
            _popupSystem.PopupEntity(Loc.GetString("Skill-issue-message-chemistry-unskilled", ("lvl", component.RequiresChemistryLevel)), user, user);
            return false;
        }
        // Проверка уровня медицины
        if (!HasSkillLevel(user, component.RequiresMedicineLevel, skillComponent => skillComponent.MedicineLevel)){
            _popupSystem.PopupEntity(Loc.GetString("Skill-issue-message-medicine-unskilled", ("lvl", component.RequiresMedicineLevel)), user, user);
            return false;
        }
        // Проверка уровня пилотирования
        if (!HasSkillLevel(user, component.RequiresPilotingLevel, skillComponent => skillComponent.PilotingLevel)){
            _popupSystem.PopupEntity(Loc.GetString("Skill-issue-message-piloting-unskilled", ("lvl", component.RequiresPilotingLevel)), user, user);
            return false;
        }
        return true;
    }

    public bool HasSkillLevel(EntityUid user, int requiredLevel, Func<SkillComponent, int> skillSelector)
    {
        if (TryComp<SkillComponent>(user, out var skillComponent) && skillSelector(skillComponent) >= requiredLevel)
            return true;
        return false;
    }
}

