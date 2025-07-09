using Content.Server.Chat.Systems;
using Content.Shared.Vanilla.Dominator;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Interaction;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Inventory;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server.Vanilla.Dominator;

public sealed class DominatorSystem : SharedDominatorSystem
{
    [Dependency] private readonly AccessReaderSystem _accessReader = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<DominatorComponent, InteractUsingEvent>(OnInteractUsing);
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
            DominatorState.NonLethal => "Текущий режим — станнер. Спокойно прицельтесь и обезвредьте цель",
            DominatorState.Lethal => "Мера наказания изменена. Текущий режим — летальный. Спокойно прицельтесь и уничтожьте цель",
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
