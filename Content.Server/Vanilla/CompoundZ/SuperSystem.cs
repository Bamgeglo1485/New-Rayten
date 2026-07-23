using Content.Server.Chat.Managers;
using Content.Shared.Chat;
using Content.Shared.Vanilla.CompoundZ;
using Robust.Server.Audio;
using Robust.Server.Player;
using Robust.Shared.Player;

namespace Content.Server.Vanilla.CompoundZ;

// Серверная система отвечает только за эффекты
public sealed partial class SuperSystem : EntitySystem
{
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private AudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SuperComponent, SuperBornEvent>(OnSuperBorn);
        SubscribeLocalEvent<SuperComponent, SuperLossEvent>(OnSuperLoss);
    }

    private void OnSuperBorn(Entity<SuperComponent> entity, ref SuperBornEvent args)
    {
        if (!TryComp<ActorComponent>(entity, out var actor))
            return;

        if (!entity.Comp.Antag)
            return;

        var superPrototype = entity.Comp.Prototype;
        if (superPrototype == null)
            return;
        
        if (entity.Comp.Prototype != null)
        {
            var message = ($"Вы теперь супер и вольны творить что угодно ради ВАШЕЙ ВЫГОДЫ. Вы в праве как стать героем, так и преследовать корыстные цели без массовых убийств. Ваша способность - {entity.Comp.Prototype.Description}");
            var wrappedMessage = Loc.GetString("chat-manager-server-wrap-message", ("message", message));
            _chat.ChatMessageToOne(ChatChannel.Server,
                      message,
                      wrappedMessage,
                      default,
                      false,
                      actor.PlayerSession.Channel,
                      colorOverride: Color.FromHex("#b81151"));
        }

        _audio.PlayGlobal(entity.Comp.BriefingSound, actor.PlayerSession);
    }

    private void OnSuperLoss(Entity<SuperComponent> entity, ref SuperLossEvent args)
    {
        if (!TryComp<ActorComponent>(entity, out var actor))
            return;

        var message = ("Ваши силы иссякают, сердце замедляется... Вы лишены сил, но не лишены прошлых слабостей.");
        var wrappedMessage = Loc.GetString("chat-manager-server-wrap-message", ("message", message));
        _chat.ChatMessageToOne(ChatChannel.Server,
                      message,
                      wrappedMessage,
                      default,
                      false,
                      actor.PlayerSession.Channel,
                      colorOverride: Color.FromHex("#b81151"));

        _audio.PlayGlobal(entity.Comp.UnsuperedSound, actor.PlayerSession);
    }
}
