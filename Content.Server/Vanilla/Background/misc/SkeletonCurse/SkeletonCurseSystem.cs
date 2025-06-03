using Content.Server.Polymorph.Systems;
using Content.Server.Speech.Components;
using Content.Server.Chat.Managers;
using Content.Server.Destructible;
using Content.Server.Destructible.Thresholds.Triggers;
using Content.Shared.CombatMode.Pacification;
using Content.Shared.Damage;
using Content.Shared.Mobs.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Chat;
using Content.Shared.Mobs;
using Robust.Shared.Prototypes;
using Robust.Shared.Player;
using System.Linq;

namespace Content.Server.Vanilla.Background.SkeletonCurse;

public sealed class SkeletonCurseSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly PolymorphSystem _polymorphSystem = default!;
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly ActorSystem _actor = default!;
    public override void Initialize()
    {
        SubscribeLocalEvent<SkeletonCurseComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<SkeletonCurseComponent, DamageChangedEvent>(OnDamageChanged, before: [typeof(MobThresholdSystem)]);
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
    /// Основной проклинающий метод
    /// </summary>
    public void Curse(EntityUid uid)
    {
        var cursedent = _polymorphSystem.PolymorphEntity(uid, "CursedSkeleton");
        if (cursedent == null)
            return;

        var cursecomp = EnsureComp<SkeletonCurseComponent>(cursedent.Value);

        var accent = EnsureComp<ReplacementAccentComponent>(cursedent.Value);
        accent.Accent = "genericAggressive";
        Dirty(cursedent.Value, accent);

        AddComp<PacifiedComponent>(cursedent.Value);
        if (TryComp<DestructibleComponent>(cursedent.Value, out var destructible))
        {
            foreach (var threshold in destructible.Thresholds)
            {
                if (threshold.Trigger is DamageTrigger damageTrigger)
                {
                    damageTrigger.Damage = 500;
                }
            }

            Dirty(cursedent.Value, destructible);
        }
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
        if (component.LifetimeDamage[origin] > FixedPoint2.New(30))
        {
            if (TryComp<DamageableComponent>(uid, out var damagecomp))
            {
                _damageable.TryChangeDamage(uid, component.Damage, true, false, damagecomp);
            }

            Curse(origin);
        }
    }
}
