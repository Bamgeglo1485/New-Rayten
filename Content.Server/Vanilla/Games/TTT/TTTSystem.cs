using Content.Shared.Damage.Systems;
using Content.Shared.Mobs;
using Content.Shared.Ghost;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Player;
using Content.Shared.Vanilla.TDM;
using Content.Shared.Vanilla.Games.TTT;
using Content.Shared.Chat;

namespace Content.Server.Vanilla.Games.TTT;

public sealed partial class TTTSystem : SharedTTTSystem
{
    [Dependency] private readonly SharedGhostSystem _ghosts = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    private EntityUid? _currentrule = null;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TTTMarkerComponent, DamageModifyEvent>(OnDamageModify);    //Урон в зависимости от крамы
        SubscribeLocalEvent<TTTMarkerComponent, DamageChangedEvent>(OnDamageChange);    //Начисляем бонусы в виде кармы за урон
        SubscribeLocalEvent<TTTMarkerComponent, MobStateChangedEvent>(OnMobStateChanged); //Вычёркиваем

        SubscribeLocalEvent<TTTRuleComponent, MapInitEvent>(OnRuleInit);//новый геймрул кайф
        SubscribeLocalEvent<TTTRuleComponent, ComponentShutdown>(OnRuleShutDown); // это конец

        SubscribeNetworkEvent<TTTInfoRequest>(OnInfoRequest); //Пользователь запросил инфы
        SubscribeNetworkEvent<TPMeToTTTEvent>(OnTTTJoinRequest); //Пользователь захотел зайти на арену
        _playerManager.PlayerStatusChanged += OnPlayerStatusChanged;
        LoadKarma();
    }
    public override void Shutdown()
    {
        base.Shutdown();
        SaveKarma();
        _playerManager.PlayerStatusChanged -= OnPlayerStatusChanged;
    }

    private void OnRuleInit(EntityUid uid, TTTRuleComponent rule, MapInitEvent args)
    {
        _currentrule = uid;
        UpdateInformation(rule);
    }

    private void OnRuleShutDown(EntityUid uid, TTTRuleComponent component, ComponentShutdown args)
    {
        GameOver(component);
        _currentrule = null;
    }
    #region  connecting
    private void OnTTTJoinRequest(TPMeToTTTEvent msg, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;

        if (session == null || _currentrule == null)
            return;

        if (!TryComp<TTTRuleComponent>(_currentrule, out var rule))
            return;

        if (rule.CurrentStatus != TTTStatus.AwaitStart)
            return;

        if (GetKarma(session.UserId) <= 0)
            return;

        if (rule.Sessions.Contains(session))
            return;

        rule.Sessions.Add(session);

        //Сообщаем о том что добавился новый игрок
        UpdateInformation(rule);
        if (session.AttachedEntity != null && TryComp<GhostComponent>(session.AttachedEntity, out var ghost))
            _ghosts.SetCanReturnToBody((session.AttachedEntity.Value, ghost), false);
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs e)
    {
        if (e.NewStatus != SessionStatus.Disconnected || _currentrule == null || !TryComp<TTTRuleComponent>(_currentrule, out var rule))
            return;

        if (!rule.Sessions.Contains(e.Session))
            return;

        rule.Sessions.Remove(e.Session);

        //Сообщаем о том что игрок ливнул с позором
        UpdateInformation(rule);
    }

    private void OnInfoRequest(TTTInfoRequest msg, EntitySessionEventArgs args)
    {
        if (_currentrule == null)
            return;

        if (!TryComp<TTTRuleComponent>(_currentrule, out var rule))
            return;
        UpdateInformation(rule, args.SenderSession);

    }
    private void UpdateInformation(TTTRuleComponent rule, ICommonSession? session = null)
    {
        var filter = session == null ? Filter.Broadcast() : Filter.SinglePlayer(session);
        var response = new TTTInformation(rule.Sessions.Count, rule.TimeForPlayersJoin, rule.CurrentStatus == TTTStatus.AwaitStart);
        RaiseNetworkEvent(response, filter);
    }
    #endregion
    ///<summary>
    /// true если оба игрока в одной команде, flase иначе
    /// </summary>
    private bool IsSameTeam(TTTRole attackerRole, TTTRole victimRole)
    {
        return attackerRole == TTTRole.Traitor && victimRole == TTTRole.Traitor ||
        attackerRole is TTTRole.Inocent or TTTRole.Detective &&
        victimRole is TTTRole.Inocent or TTTRole.Detective;
    }
    /// <summary>
    /// подтверждаем смерть игрока
    /// </summary>
    protected override void ConfirmDead(Entity<TTTDeadBodyComponent> target, EntityUid user)
    {
        if (!TryComp<TTTRuleComponent>(target.Comp.RuleLink, out var rule))
            return;
        if (!TryComp<TTTMarkerComponent>(target, out var marker))
            return;

        var message = Loc.GetString("ttt-confirm-dead", ("user", Name(user)), ("target", Name(target)), ("role", marker.GetUIRoleName()));
        var wrappedMessage = Loc.GetString("chat-manager-server-wrap-message", ("message", message));

        _chatManager.ChatMessageToManyFiltered(
            Filter.Empty().AddPlayers(rule.Sessions),
            ChatChannel.Radio,
            message,
            wrappedMessage,
            EntityUid.Invalid,
            hideChat: false,
            recordReplay: true,
            colorOverride: marker.GetColor()
        );
    }
}
