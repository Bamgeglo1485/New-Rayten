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

namespace Content.Client.Vanilla.SocialVerb
{
    public sealed class ClientSocialVerbSystem : EntitySystem
    {
        [Dependency] private readonly IPrototypeManager _proto = default!;
        [Dependency] private readonly SharedHandsSystem _hands = default!;
        [Dependency] private readonly IGameTiming _timing = default!;

        private TimeSpan _nextvalidtime = TimeSpan.Zero;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<GetVerbsEvent<Verb>>(OnGetVerbs);
        }

        private void OnGetVerbs(GetVerbsEvent<Verb> args)
        {
            if (!HasComp<MobStateComponent>(args.User) || !HasComp<MobStateComponent>(args.Target))
                return;

            if (!args.CanInteract)
                return;

            var disabled = _nextvalidtime > _timing.CurTime;
            var item = _hands.GetActiveItem(args.User);

            foreach (var proto in _proto.EnumeratePrototypes<SocialVerbPrototype>())
            {
                if (proto.RequiresInteractRange && !args.CanAccess)
                    continue;

                if (proto.RequiresActiveItem && item == null)
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
                    RaiseNetworkEvent(new SocialVerbEvent(proto.ID, GetNetEntity(args.Target), GetNetEntity(item)));
                }
            };
        }

        private void MakeVerb(string id, EntityUid user, EntityUid target, EntityUid? item)
        {
            switch (id)
            {
                case "Scream":
                    //Окрикнуть
                    //Большой попуп на таргете что такой-то такой-то окрикнул вас
                    //сообщение в чате от юзера либо роль либо имя
                    //и в чате
                    break;
                case "Wave":
                    //Окрикнуть
                    //Маленький попуп над бошкой юзера что он помахал такому-то челу
                    //и в чате
                    break;
                case "Shout"://потрясти
                    //Окрикнуть
                    //Маленький попуп над бошкой юзера что он помахал такому-то челу
                    //и в чате
                    break;
                case "OfferAHand":
                    //Протянуть руку
                    //Эмоция что такой-то протягивает руку
                    //у чела появляется окошко типа пожать руку? да/нет
                    //если пожали в чате появляется эмоция что такие-то челы пожали руки
                    break;
                case "OfferAnItem":
                    //Предложить предмет
                    break;
                default:
                    Log.Warning($"Неизвестный social verb ID: {id}");
                    break;
            }
        }

    }
}