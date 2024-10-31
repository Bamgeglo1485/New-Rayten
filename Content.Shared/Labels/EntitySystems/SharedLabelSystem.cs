using Content.Shared.Examine;
using Content.Shared.Labels.Components;
using Content.Shared.NameModifier.EntitySystems;
using Robust.Shared.Utility;
using Content.Shared.Vanilla.Skill;

namespace Content.Shared.Labels.EntitySystems;

public abstract partial class SharedLabelSystem : EntitySystem
{
    [Dependency] protected readonly NameModifierSystem NameMod = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LabelComponent, MapInitEvent>(OnLabelCompMapInit);
        SubscribeLocalEvent<LabelComponent, ExaminedEvent>(OnExamine);
        //SubscribeLocalEvent<LabelComponent, RefreshNameModifiersEvent>(OnRefreshNameModifiers);
    }

    private void OnLabelCompMapInit(EntityUid uid, LabelComponent component, MapInitEvent args)
    {
        if (!string.IsNullOrEmpty(component.CurrentLabel))
        {
            component.CurrentLabel = Loc.GetString(component.CurrentLabel);
            Dirty(uid, component);
        }

        NameMod.RefreshNameModifiers(uid);
    }

    public virtual void Label(EntityUid uid, string? text, MetaDataComponent? metadata = null, LabelComponent? label = null){}

    private void OnExamine(EntityUid uid, LabelComponent? label, ExaminedEvent args)
    {
        if (!Resolve(uid, ref label))
            return;

        if (label.CurrentLabel == null)
            return;

        var message = new FormattedMessage();

        //vanilla-station-skill-issue-start
        if(EntityManager.TryGetComponent<RequiresSkillComponent>(uid, out var skillRequirements) && EntityManager.TryGetComponent<SkillComponent>(args.Examiner, out var skill)){
            if (!HasSkillLevel(args.Examiner, skillRequirements.RequiresChemistryLevelToRead, skillComponent => skillComponent.ChemistryLevel)
            || !HasSkillLevel(args.Examiner, skillRequirements.RequiresMedicineLevelToRead, skillComponent => skillComponent.MedicineLevel))
                return;
        }
        //vanilla-station-skill-issue-end
        message.AddMarkup(Loc.GetString("hand-labeler-has-label", ("label", label.CurrentLabel)));
        args.PushMessage(message);
    }

    //vanilla-station-skill-issue
    public bool HasSkillLevel(EntityUid user, int requiredLevel, Func<SkillComponent, int> skillSelector)
    {
        if (TryComp<SkillComponent>(user, out var skillComponent) && skillSelector(skillComponent) >= requiredLevel)
            return true;
        return false;
    }

    // private void OnRefreshNameModifiers(Entity<LabelComponent> entity, ref RefreshNameModifiersEvent args)
    // {
    //     if (!string.IsNullOrEmpty(entity.Comp.CurrentLabel))
    //         args.AddModifier("comp-label-format", extraArgs: ("label", entity.Comp.CurrentLabel));
    // }
}
