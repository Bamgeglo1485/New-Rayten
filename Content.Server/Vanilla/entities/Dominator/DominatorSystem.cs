using Content.Server.Chat.Systems;
using Content.Server.Ghost.Roles;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Ghost;
using Content.Shared.Vanilla.Dominator;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Interaction;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Inventory;
using Content.Shared.DoAfter;
using Content.Shared.PDA;
using Content.Shared.Mind;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using Robust.Shared.Containers;
using Robust.Shared.Random;
using Robust.Shared.Prototypes;

namespace Content.Server.Vanilla.Dominator;

public sealed class DominatorSystem : SharedDominatorSystem
{
    [Dependency] private readonly AccessReaderSystem _accessReader = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly GhostSystem _ghost = default!;
    [Dependency] private readonly GhostRoleSystem _ghostrole = default!;

    private const float ScanDoAfterDuration = 5f;
    private const float ScanCoolDown = 10f; //в минутах

    private Dictionary<EntityUid, TimeSpan> _scannedEntities = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<DominatorComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<DominatorComponent, AfterInteractEvent>(OnScannerAfterInteract);
        SubscribeLocalEvent<DominatorComponent, DominatorDoAfterEvent>(OnDoAfter);
        SubscribeLocalEvent<DominatorComponent, GetVerbsEvent<Verb>>(OnGetVerbs);
    }
    private void OnGetVerbs(EntityUid uid, DominatorComponent dom, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanInteract || args.Hands == null)
            return;

        if (!TryComp<GhostRoleComponent>(uid, out var ghostRole))
            return;

        args.Verbs.Add(new Verb
        {
            DoContactInteraction = true,
            Text = dom.AllowGhostTakeover
                ? Loc.GetString("dominator-verb-disable-ghost")
                : Loc.GetString("dominator-verb-enable-ghost"),
            Act = () => ChangeAI(uid, dom, ghostRole)
        });
    }

    private void ChangeAI(EntityUid uid, DominatorComponent dom, GhostRoleComponent ghostRole)
    {
        dom.AllowGhostTakeover = !dom.AllowGhostTakeover;

        if (dom.AllowGhostTakeover)
        {
            _ghostrole.RegisterGhostRole((uid, ghostRole));
        }
        else
        {
            // Если в доминаторе сидит игрок — выгнать в ghost
            if (_mind.TryGetMind(uid, out var mindId, out var mind))
                _ghost.OnGhostAttempt(mindId, false, true, true, mind);

            _ghostrole.UnregisterGhostRole((uid, ghostRole));
        }
    }

    private void OnScannerAfterInteract(EntityUid uid, DominatorComponent component, AfterInteractEvent args)
    {
        if (args.Target is not { } target)
            return;

        if (!HasComp<DangerMobComponent>(target))
            return;

        if (!args.CanReach)
            return;

        var curTime = _timing.CurTime;

        // Проверяем был ли уже скан
        if (_scannedEntities.TryGetValue(target, out var lastScan))
        {
            // Кулдаун 5 минут
            var cooldown = TimeSpan.FromMinutes(ScanCoolDown);
            var remaining = (lastScan + cooldown) - curTime;

            if (remaining > TimeSpan.Zero)
            {
                string timeText;
                if (remaining.TotalMinutes >= 1)
                {
                    if (remaining.Seconds > 0)
                        timeText = $"{remaining.Minutes} мин. {remaining.Seconds} сек.";
                    else
                        timeText = $"{remaining.Minutes} мин.";
                }
                else
                {
                    timeText = $"{remaining.Seconds} сек.";
                }

                if (CanSay(component))
                    _chat.TrySendInGameICMessage(uid,
                        Loc.GetString("dominator-scanner-cooldown", ("time", timeText)),
                        InGameICChatType.Speak, true);

                return;
            }
        }
        if (CanSay(component))
            _chat.TrySendInGameICMessage(uid, Loc.GetString("dominator-scanner-start"), InGameICChatType.Speak, true);

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, ScanDoAfterDuration, new DominatorDoAfterEvent(), uid, target: target, used: uid)
        {
            DistanceThreshold = 2f
        });
    }

    private void OnDoAfter(EntityUid uid, DominatorComponent component, DoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Args.Target == null)
            return;

        _audio.PlayPvs(component.CompleteSound, uid);

        int targetdanger = _dangermob.GetEntityDanger(args.Args.Target.Value, deepseek: true);

        _chat.TrySendInGameICMessage(uid, Loc.GetString("dominator-scanner-end", ("danger", targetdanger)), InGameICChatType.Speak, true);

        // Обновляем время скана (или добавляем нового)
        _scannedEntities[args.Args.Target.Value] = _timing.CurTime;
        args.Handled = true;
    }

    public override void UpdateWeaponMode(EntityUid uid, DominatorComponent dom, DominatorState newMode)
    {
        base.UpdateWeaponMode(uid, dom, newMode);

        if (newMode != DominatorState.NonLethal && newMode != DominatorState.Lethal)
            return;

        if (!CanSay(dom))
            return;

        string message = newMode switch
        {
            DominatorState.Disabled => "Деактивация",
            DominatorState.NonLethal => "Текущий режим — станнер.",
            DominatorState.Lethal => "Текущий режим — летальный.",
            _ => "Обнаружен неизвестный режим. Ошибка."
        };

        _chat.TrySendInGameICMessage(uid, message, InGameICChatType.Speak, true);

    }

    private bool CanSay(DominatorComponent comp)
    {
        var curtime = _timing.CurTime;

        if (comp.NextSpeechTime > curtime)
            return false;

        comp.NextSpeechTime = curtime + TimeSpan.FromSeconds(10f);
        return true;
    }
    private void OnInteractUsing(EntityUid uid, DominatorComponent comp, InteractUsingEvent args)
    {
        var used = args.Used;
        var user = args.User;

        if (!TryComp<IdCardComponent>(used, out var idCard))
            return;

        if (!TryComp<AccessComponent>(used, out var access))
            return;

        if (!TryComp<AccessReaderComponent>(uid, out var accessReader))
            return;

        // Повторное использование той же карты — сброс авторизации
        if (comp.AuthorizedID == used)
        {
            if (CanSay(comp))
                _chat.TrySendInGameICMessage(uid, Loc.GetString("dominator-auth-cleared"), InGameICChatType.Speak, true);

            comp.AuthorizedID = null;
            Dirty(uid, comp);
            args.Handled = true;
            return;
        }

        // авторизация уже авторизованного доминатора
        if (comp.AuthorizedID != null)
        {
            if (CanSay(comp))
                _chat.TrySendInGameICMessage(uid, Loc.GetString("dominator-auth-already-auth"), InGameICChatType.Speak, true);
            return;
        }

        // Вынесенная авторизация
        if (TryAuthorize(uid, comp, used, idCard, accessReader))
        {
            args.Handled = true;
        }
    }

    private bool TryAuthorize(EntityUid uid, DominatorComponent comp, EntityUid used, IdCardComponent idCard, AccessReaderComponent accessReader)
    {
        var sources = new HashSet<EntityUid> { used };
        var accessTags = _accessReader.FindAccessTags(used, sources);
        _accessReader.FindStationRecordKeys(used, out var stationKeys, sources);

        if (!_accessReader.IsAllowed(accessTags, stationKeys, uid, accessReader))
        {
            if (CanSay(comp))
                _chat.TrySendInGameICMessage(uid, Loc.GetString("dominator-auth-notallowed"), InGameICChatType.Speak, true);
            return false;
        }

        var name = idCard.FullName ?? Loc.GetString("Неизвестный пользователь");

        if (CanSay(comp))
        {
            var dataset = _proto.Index(comp.Dataset);
            var pick = _random.Pick(dataset.Values);
            _chat.TrySendInGameICMessage(uid, Loc.GetString(pick, ("name", name)), InGameICChatType.Speak, true);
        }

        comp.AuthorizedID = used;
        Dirty(uid, comp);

        return true;
    }
}
