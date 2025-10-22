using Content.Shared.Verbs;
using Content.Shared.Mobs.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.IdentityManagement;
using Content.Shared.Vanilla.SocialVerb;

using Robust.Shared.Player;
using Robust.Shared.Network;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

using Content.Client.Vanilla.TDM.UI;

namespace Content.Client.Vanilla.SocialVerb;

public sealed class ClientSocialVerbSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private TimeSpan _nextvalidtime = TimeSpan.Zero;

    private SimpleAcceptWindow? _window;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GetVerbsEvent<Verb>>(OnGetVerbs);
        SubscribeNetworkEvent<SocialVerbEvent>(OnGetMsg);
    }

    private void OnGetMsg(SocialVerbEvent msg)
    {
        var target = GetEntity(msg.Target);
        var item = GetEntity(msg.Item);
        var user = GetEntity(msg.User);

        if (user == null)
            return;

        MakeWindow(msg.ID, user.Value, target, item);
    }

    private void MakeWindow(string id, EntityUid user, EntityUid target, EntityUid? item)
    {
        if (_window != null && _window.IsOpen)
        {
            _window.Dispose();
            _window = null;
        }

        var sourcename = Identity.Name(user, EntityManager, target);
        var itemname = item != null ? Identity.Name(item.Value, EntityManager, target) : "руку";

        _window = new SimpleAcceptWindow(Loc.GetString($"social-verb-window-title-{id}"),
                                            Loc.GetString($"social-verb-window-text-{id}", ("item", itemname), ("user", sourcename)),
                                            Loc.GetString($"social-verb-window-accept-button-{id}"),
                                            Loc.GetString($"social-verb-window-deny-button-{id}"));

        _window.AcceptButton.OnPressed += _ =>
        {
            _window.Dispose();
            _window = null;
            RaiseNetworkEvent(new SocialVerbEvent(id, GetNetEntity(user), GetNetEntity(item), isResponse: true));
        };

        _window.DenyButton.OnPressed += _ =>
        {
            _window.Dispose();
            _window = null;
        };

        _window.OpenCentered();
    }

    private void OnGetVerbs(GetVerbsEvent<Verb> args)
    {
        if (!HasComp<MobStateComponent>(args.User) || !HasComp<MobStateComponent>(args.Target))
            return;
        if (!HasComp<ActorComponent>(args.Target))
            return;
        if (args.User == args.Target)
            return;

        if (!args.CanInteract)
            return;

        var disabled = _nextvalidtime > _timing.CurTime;
        var item = _hands.GetActiveItem(args.User);

        foreach (var proto in _proto.EnumeratePrototypes<SocialVerbPrototype>())
        {
            if (proto.RequiresInteractRange && !args.CanAccess)
                continue;

            if (proto.RequiresActiveItem && (item == null || !_hands.CanPickupAnyHand(args.Target, item.Value)))
                continue;

            args.Verbs.Add(BuildVerb(proto, args, item, disabled));
        }
    }

    private Verb BuildVerb(SocialVerbPrototype proto, GetVerbsEvent<Verb> args, EntityUid? item, bool disabled)
    {
        string text = proto.Text;

        if (item != null && proto.RequiresActiveItem)
        {
            var itemName = Identity.Name(item.Value, EntityManager, args.User);
            text = $"{proto.Text} {itemName}";
        }

        return new Verb
        {
            Text = text,
            Category = VerbCategory.Social,
            Priority = 1,
            Disabled = disabled,
            ClientExclusive = true,
            Icon = proto.Icon is not null
                ? new SpriteSpecifier.Texture(new ResPath(proto.Icon))
                : null,
            Act = () =>
            {
                _nextvalidtime = _timing.CurTime + TimeSpan.FromSeconds(5);
                if (!proto.RequiresActiveItem)
                    RaiseNetworkEvent(new SocialVerbEvent(proto.ID, GetNetEntity(args.Target), null, GetNetEntity(args.User)));
                else if (item != null && item == _hands.GetActiveItem(args.User))
                    RaiseNetworkEvent(new SocialVerbEvent(proto.ID, GetNetEntity(args.Target), GetNetEntity(item.Value), GetNetEntity(args.User)));
            }
        };
    }
}