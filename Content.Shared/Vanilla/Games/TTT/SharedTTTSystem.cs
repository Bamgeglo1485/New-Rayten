using Content.Shared.Interaction;
using Content.Shared.Verbs;
using Robust.Shared.Utility;
namespace Content.Shared.Vanilla.Games.TTT;

public abstract class SharedTTTSystem : EntitySystem
{
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TTTDeadBodyComponent, GetVerbsEvent<InteractionVerb>>(AddExamineVerb);
        SubscribeLocalEvent<TTTDeadBodyComponent, ActivateInWorldEvent>(OnActivated);
    }
    private void OnActivated(EntityUid uid, TTTDeadBodyComponent component, ActivateInWorldEvent args)
    {
        OpenBodyExamineUi(args.User, (uid, component));
    }
    private void AddExamineVerb(EntityUid uid, TTTDeadBodyComponent component, GetVerbsEvent<InteractionVerb> args)
    {
        if (args.Hands == null || !args.CanAccess || !args.CanInteract || args.Target == args.User)
            return;

        InteractionVerb verb = new()
        {
            Text = Loc.GetString("ttt-verb-examine-body-text"),
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/outfit.svg.192dpi.png")),
            Act = () => OpenBodyExamineUi(args.User, (uid, component)),
        };

        args.Verbs.Add(verb);
    }
    public void OpenBodyExamineUi(EntityUid user, Entity<TTTDeadBodyComponent> target)
    {
        _ui.OpenUi(target.Owner, BodyExamineUiKey.Key, user);
        if (TryComp<TTTMarkerComponent>(user, out var marker))
        {
            if (!target.Comp.Confirmed)
            {
                ConfirmDead(target, user);
                target.Comp.Confirmed = true;
                Dirty(target);
            }
        }
    }
    protected virtual void ConfirmDead(Entity<TTTDeadBodyComponent> target, EntityUid user)
    {

    }
}
