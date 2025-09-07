using Content.Server.Chat.Systems;
using Content.Shared.Vanilla.Dominator;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Interaction;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Inventory;
using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server.Vanilla.Dominator;

public sealed class DominatorSystem : SharedDominatorSystem
{
    [Dependency] private readonly AccessReaderSystem _accessReader = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    private const float ScanDoAfterDuration = 5f;
    private SoundSpecifier? CompleteSound = new SoundPathSpecifier("/Audio/Items/beep.ogg");

    public override void Initialize()
    {
        SubscribeLocalEvent<DominatorComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<DominatorComponent, AfterInteractEvent>(OnScannerAfterInteract);
        SubscribeLocalEvent<DominatorComponent, DominatorDoAfterEvent>(OnDoAfter);
    }

    private void OnScannerAfterInteract(EntityUid uid, DominatorComponent component, AfterInteractEvent args)
    {
        if (args.Target is not { } target)
            return;

        if (!HasComp<DangerMobComponent>(target))
            return;

        if (!args.CanReach)
            return;

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
        _audio.PlayPvs(CompleteSound, uid);
        int targetdanger = _dangermob.GetEntityDanger(args.Args.Target.Value, deepseek: true);

        _chat.TrySendInGameICMessage(uid, Loc.GetString("dominator-scanner-end", ("danger", targetdanger)), InGameICChatType.Speak, true);

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

        //авторизация уже авторизованного доминатора
        if (comp.AuthorizedID != null)
        {
            if (CanSay(comp))
                _chat.TrySendInGameICMessage(uid, Loc.GetString("dominator-auth-already-auth"), InGameICChatType.Speak, true);

            return;
        }


        var sources = new HashSet<EntityUid> { used };
        var accessTags = _accessReader.FindAccessTags(used, sources);
        _accessReader.FindStationRecordKeys(used, out var stationKeys, sources);

        if (!_accessReader.IsAllowed(accessTags, stationKeys, uid, accessReader))
        {
            if (CanSay(comp))
                _chat.TrySendInGameICMessage(uid, Loc.GetString("dominator-auth-notallowed"), InGameICChatType.Speak, true);
            return;
        }

        var name = idCard.FullName ?? Loc.GetString("Неизвестный пользователь");

        if (CanSay(comp))
            _chat.TrySendInGameICMessage(uid, Loc.GetString("dominator-auth-success", ("name", name)), InGameICChatType.Speak, true);

        comp.AuthorizedID = used;
        Dirty(uid, comp);
        args.Handled = true;
    }

}
