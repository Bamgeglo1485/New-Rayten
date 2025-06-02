
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
using Robust.Shared.Prototypes;
using Robust.Shared.Player;

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
        SubscribeLocalEvent<SkeletonCurseComponent, DamageChangedEvent>(OnDamageChanged, before: [typeof(MobThresholdSystem)]);
    }
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
            //Превращаем в тупого-скелета-пацифиста
            var cursedent = _polymorphSystem.PolymorphEntity(origin, "CursedSkeleton");
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
    }
}
