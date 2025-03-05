using Content.Shared.Vanilla.Skill;
using Content.Shared.UserInterface;
using Content.Shared.Interaction;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Interaction.Events;
using Content.Shared.Hands;
namespace Content.Client.Vanilla.Skill;

public sealed class RequiresSkillSystem : SharedRequiresSkillSystem
{
    protected override void OnActivate(EntityUid uid, RequiresSkillComponent component, ref ActivatableUIOpenAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        var hasSkills = HasRequiredSkills(args.User, component, true);

        var hasCraftSkills = !component.NeedCraftableSkills || HasRequiredSkillsForCraft(args.User, component, true);

        if (hasSkills && hasCraftSkills)
            return;

        args.Cancel();
    }
    protected override void OnSkillCheckToActivateInWorld(EntityUid uid, RequiresSkillToActivateInWorldComponent component, ref ActivateInWorldEvent args)
    {
        if (args.Handled)
            return;
        if(!EntityManager.TryGetComponent<RequiresSkillComponent>(uid, out var Reqcomponent))
            return;
        if(HasRequiredSkills(args.User, Reqcomponent, true))
            return;
        args.Handled = true;
        args.Complex = false;
    }

    protected override void OnItemSlotInsertAttempt(EntityUid uid, RequiresSkillComponent component, ref ItemSlotInsertAttemptEvent args)
    {
        if (args.Cancelled || args.User == null)
            return;

        var hasSkills = HasRequiredSkills(args.User.Value, component, true);

        var hasCraftSkills = !component.NeedCraftableSkills || HasRequiredSkillsForCraft(args.User.Value, component, true);

        if (hasSkills && hasCraftSkills)
            return;

        args.Cancelled = true;
    }
    protected override void OnItemSlotEjectAttempt(EntityUid uid, RequiresSkillComponent component, ref ItemSlotEjectAttemptEvent args)
    {
        if (args.Cancelled || args.User == null)
            return;

        var hasSkills = HasRequiredSkills(args.User.Value, component, true);

        var hasCraftSkills = !component.NeedCraftableSkills || HasRequiredSkillsForCraft(args.User.Value, component, true);

        if (hasSkills && hasCraftSkills)
            return;

        args.Cancelled = true;
    }
}