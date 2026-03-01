using Content.Server.Polymorph.Systems;
using Content.Server.Chat.Managers;
using Content.Shared.Damage.Systems;
using Content.Shared.Vanilla.Damage.Events;
using Content.Shared.FixedPoint;
using Content.Shared.Chat;
using Robust.Shared.Player;
using Content.Shared.Gibbing;

namespace Content.Server.Vanilla.Background.SkeletonCurse;

public sealed class SkeletonCurseSystem : EntitySystem
{
    [Dependency] private readonly GibbingSystem _gibbing = default!;
    [Dependency] private readonly PolymorphSystem _polymorphSystem = default!;
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly ActorSystem _actor = default!;
    public override void Initialize()
    {
        SubscribeLocalEvent<SkeletonCurseComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<SkeletonCurseComponent, StaminaCritEvent>(OnStamCrit);
        SubscribeLocalEvent<SkeletonCurseComponent, DamageChangedEvent>(OnDamageChanged);
    }
    /// <summary>
    /// Получили проклятие - даём небольшой брифинг в чат
    /// </summary>
    private void OnMapInit(EntityUid uid, SkeletonCurseComponent component, MapInitEvent args)
    {
        if (!_actor.TryGetSession(uid, out var session) || session == null)
            return;

        var message = Loc.GetString("SkeletonCurse-brief", ("color", Color.Violet));
        var wrappedMessage = Loc.GetString("chat-manager-server-wrap-message", ("message", message));
        _chat.ChatMessageToOne(
            ChatChannel.Server,
            message,
            wrappedMessage,
            default,
            false,
            session.Channel
        );
    }
    /// <summary>
    /// Запоминаем всех наших обидчиков, если урон превышает 30 - переносим проклятие
    /// </summary>
    private void OnDamageChanged(EntityUid uid, SkeletonCurseComponent component, DamageChangedEvent args)
    {
        if (!args.DamageIncreased || args.DamageDelta == null || args.Origin == null || args.Origin.Value == uid)
            return;

        var origin = args.Origin.Value;

        if (!HasComp<ActorComponent>(origin))
            return;

        var damage = args.DamageDelta.GetTotal();
        component.LifetimeDamage[origin] = component.LifetimeDamage.GetValueOrDefault(origin) + damage;
        if (component.LifetimeDamage[origin] > FixedPoint2.New(10))
        {
            _gibbing.Gib(uid, user: origin);
            _polymorphSystem.PolymorphEntity(origin, "CursedSkeleton");
        }
    }
    private void OnStamCrit(EntityUid uid, SkeletonCurseComponent component, StaminaCritEvent args)
    {
        _gibbing.Gib(uid);
    }
}
