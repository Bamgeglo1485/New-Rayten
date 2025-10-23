using Content.Shared.Vanilla.SocialVerb;
using Content.Shared.IdentityManagement;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Storage;
using Content.Shared.Chat;
using Content.Server.Chat.Systems;
using Content.Server.Jittering;
using Robust.Shared.Player;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using System.Numerics;

namespace Content.Server.Vanilla.SocialVerb;

public sealed class ServerSocialVerbSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly JitteringSystem _jitter = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<SocialVerbEvent>(OnGetMsg);
    }

    private void OnGetMsg(SocialVerbEvent msg, EntitySessionEventArgs args)
    {
        if (!args.SenderSession.AttachedEntity.HasValue)
            return;

        if (!_proto.HasIndex<SocialVerbPrototype>(msg.ID))
            return;

        var target = GetEntity(msg.Target);
        var item = GetEntity(msg.Item);
        var user = args.SenderSession.AttachedEntity.Value;

        if(msg.IsResponse)
        {
            MakeResponse(msg.ID, user, target, item);
        }
        else
        {
            MakeVerb(msg.ID, user, target, item);
        }

    }

    private void MakeResponse(string id, EntityUid user, EntityUid target, EntityUid? item)
    {
        var sourcename = Identity.Name(user, EntityManager, target);
        var targetname = Identity.Name(target, EntityManager, user);
        switch (id)
        {
            case "OfferAHand":
                _chat.TrySendInGameICMessage(user, $"Жмёт руку {targetname}", InGameICChatType.Emote, hideChat: false);
                _chat.TrySendInGameICMessage(target, $"Жмёт руку {sourcename}", InGameICChatType.Emote, hideChat: false);
                break;
            case "OfferAnItem":
                if (item == null)
                    return;

                var activeItem = _hands.GetActiveItem(target);

                if (item != activeItem)
                    return;

                if (!_hands.CanPickupAnyHand(user, item.Value))
                    return;

                if(!_hands.TryDrop(target, item.Value))
                    return;

                _hands.TryPickupAnyHand(user, item.Value);
                break;
            default:
                Log.Warning($"Неизвестный social verb ID: {id}");
                break;
        }
    }

    private void MakeVerb(string id, EntityUid user, EntityUid target, EntityUid? item)
    {
        if (!TryComp<ActorComponent>(target, out var actor))
            return;
        var sourcename = Identity.Name(user, EntityManager, target);
        var targetname = Identity.Name(target, EntityManager, user);
        switch (id)
        {
            case "Scream":
                _popup.PopupEntity($"{sourcename} кричит вам!", target, target, PopupType.LargeCaution);
                _chat.TrySendInGameICMessage(user, GenerateScreamMessage(targetname), InGameICChatType.Speak, hideChat: false);
                break;
            case "Wave":
                _chat.TrySendInGameICMessage(user, $"машет {targetname}", InGameICChatType.Emote, hideChat: false);
                break;
            case "Shout":
                _chat.TrySendInGameICMessage(user, $"трясёт {targetname}", InGameICChatType.Emote, hideChat: false);
                _jitter.DoJitter(target, TimeSpan.FromSeconds(1), true);
                break;
            case "OfferAHand":
                _chat.TrySendInGameICMessage(user, $"протягивает руку", InGameICChatType.Emote, hideChat: false);
                RaiseNetworkEvent(new SocialVerbEvent(id, GetNetEntity(target), null, GetNetEntity(user)), actor.PlayerSession);
                break;
            case "OfferAnItem":
                RaiseNetworkEvent(new SocialVerbEvent(id, GetNetEntity(target), GetNetEntity(item), GetNetEntity(user)), actor.PlayerSession);
                break;
            default:
                Log.Warning($"Неизвестный social verb ID: {id}");
                break;
        }
    }

    public string GenerateScreamMessage(string targetname)
    {
        return $"ЭЙ! {targetname}";
    }

}