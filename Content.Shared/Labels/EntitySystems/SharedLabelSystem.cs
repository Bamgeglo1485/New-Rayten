using Content.Shared.Examine;
using Content.Shared.Labels.Components;
using Content.Shared.NameModifier.EntitySystems;
using Robust.Shared.Utility;
using Content.Shared.Vanilla.Skill;

namespace Content.Shared.Labels.EntitySystems;

public abstract partial class SharedLabelSystem : EntitySystem
{
    [Dependency] protected readonly NameModifierSystem NameMod = default!;
    [Dependency] private readonly SharedRequiresSkillSystem _requiresSkillSystem = default!;
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
        //Vanilla-Station-START
        if (EntityManager.TryGetComponent<RequiresSkillComponent>(uid, out var RequiresSkillComponent))
        {
            if(!_requiresSkillSystem.HasRequiredSkills(args.Examiner, RequiresSkillComponent, true))
            return;
        }
        //Vanilla-Sttion-END
        message.AddMarkup(Loc.GetString("hand-labeler-has-label", ("label", label.CurrentLabel)));
        args.PushMessage(message);
    }


    // private void OnRefreshNameModifiers(Entity<LabelComponent> entity, ref RefreshNameModifiersEvent args)
    // {
    //     if (!string.IsNullOrEmpty(entity.Comp.CurrentLabel))
    //         args.AddModifier("comp-label-format", extraArgs: ("label", entity.Comp.CurrentLabel));
    // }
}
