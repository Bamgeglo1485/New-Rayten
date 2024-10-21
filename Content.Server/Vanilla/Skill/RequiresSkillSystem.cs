using Content.Shared.UserInterface;
using Content.Shared.Vanilla.Skill;
using ActivatableUISystem = Content.Shared.UserInterface.ActivatableUISystem;

namespace Content.Server.Vanilla.Skill;

public sealed class RequiresSkillSystem : SharedRequiresSkillSystem
{
    [Dependency] private readonly ActivatableUISystem _activatableUI = default!;

    protected override void OnActivate(EntityUid uid, RequiresSkillComponent component, ref ActivatableUIOpenAttemptEvent args)
    {
        if (args.Cancelled || HasRequiredSkills(args.User, component))
        {
            return;
        }

        args.Cancel();
    }


    private bool HasRequiredSkills(EntityUid user, RequiresSkillComponent component)
    {
        // Проверка уровня химии
        if (!HasSkillLevel(user, component.RequiresChemistryLevel, skillComponent => skillComponent.ChemistryLevel))
        {
            return false;
        }
        if (!HasSkillLevel(user, component.RequiresMedicineLevel, skillComponent => skillComponent.MedicineLevel))
        {
            return false;
        }
        if (!HasSkillLevel(user, component.RequiresPilotingLevel, skillComponent => skillComponent.PilotingLevel))
        {
            return false;
        }
        return true;
    }

    private bool HasSkillLevel(EntityUid user, int requiredLevel, Func<SkillComponent, int> skillSelector)
    {
        if (TryComp<SkillComponent>(user, out var skillComponent) && skillSelector(skillComponent) >= requiredLevel)
        {
            return true;
        }
        return false;
    }
}

