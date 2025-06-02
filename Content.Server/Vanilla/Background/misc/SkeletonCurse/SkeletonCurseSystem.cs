
using Content.Server.Mind;
using Content.Server.Roles;
using Content.Server.Polymorph.Systems;
using Content.Server.Speech.Components;
using Content.Server.Chat.Managers;
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
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly RoleSystem _role = default!;
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly ActorSystem _actor = default!;
    public override void Initialize()
    {
        SubscribeLocalEvent<SkeletonCurseComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<SkeletonCurseComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<SkeletonCurseComponent, DamageChangedEvent>(OnDamageChanged, before: [typeof(MobThresholdSystem)]);
    }
    /// <summary>
    /// При смерти проклинаем того, кто нанёс больше всего урона
    /// </summary>
    private void OnMobStateChanged(EntityUid uid, SkeletonCurseComponent component, MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Critical && args.OldMobState < args.NewMobState)
        {
            if (TryComp<DamageableComponent>(uid, out var damage))
            {
                _damageable.TryChangeDamage(uid, component.Damage, true, false, damage);
            }
            else return;
        }
        else return;

        var topDamager = component.LifetimeDamage
            .OrderByDescending(pair => pair.Value)
            .FirstOrDefault().Key;

        Curse(topDamager);
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

        AddComp<SkeletonCurseComponent>(cursedent.Value);
        var accent = EnsureComp<ReplacementAccentComponent>(cursedent.Value);
        accent.Accent = "genericAggressive";
        Dirty(cursedent.Value, accent);

        AddComp<PacifiedComponent>(cursedent.Value);

        //соло-антажность
        if (_mind.TryGetMind(cursedent.Value, out var mindId, out var mindcomp))
        {
            List<EntProtoId> MindRoles = new() { "MindRoleGhostRoleSoloAntagonist" };
            _role.MindAddRoles(mindId, MindRoles, mindcomp);
        }
    }
    /// <summary>
    /// Запоминаем всех наших обидчиков, если урон превышает 100 - переносим проклятие
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
        if (component.LifetimeDamage[origin] > FixedPoint2.New(100))
        {
            if (TryComp<DamageableComponent>(uid, out var damagecomp))
            {
                _damageable.TryChangeDamage(uid, component.Damage, true, false, damagecomp);
            }

            Curse(origin);
        }
    }
}
