using Content.Shared.Popups;
using Content.Shared.Vanilla.Skill;
using Content.Shared.UserInterface;

namespace Content.Client.Vanilla.Skill;

public sealed class RequiresSkillSystem : SharedRequiresSkillSystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    protected override void OnActivate(EntityUid uid, RequiresSkillComponent component, ref ActivatableUIOpenAttemptEvent args)
    {
        if (args.Cancelled || HasRequiredSkills(args.User, component))
        {
            return;
        }

        _popup.PopupClient(Loc.GetString("skill-requirement-failed-message", ("machine", uid)), args.User, args.User);
        args.Cancel();
    }

    private bool HasRequiredSkills(EntityUid user, RequiresSkillComponent component)
    {
        // Проверка уровня химии
        if (!HasSkillLevel(user, component.RequiresChemistryLevel, skillComponent => skillComponent.ChemistryLevel))
        {
            return false;
        }
        // Проверка уровня медицины
        if (!HasSkillLevel(user, component.RequiresMedicineLevel, skillComponent => skillComponent.MedicineLevel))
        {
            return false;
        }
        // Проверка уровня пилотирования
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
