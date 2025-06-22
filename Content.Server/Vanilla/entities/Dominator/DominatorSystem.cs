using Content.Server.Chat.Systems;
using Content.Shared.Vanilla.Dominator;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Interaction;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Inventory;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server.Vanilla.Dominator;

public sealed class DominatorSystem : SharedDominatorSystem
{
    [Dependency] private readonly AccessReaderSystem _accessReader = default!;
    [Dependency] private readonly ChatSystem _chat = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<DominatorComponent, InteractUsingEvent>(OnInteractUsing);
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
            _chat.TrySendInGameICMessage(uid, Loc.GetString("dominator-auth-cleared"), InGameICChatType.Speak, true);
            comp.AuthorizedID = null;
            Dirty(uid, comp);
            args.Handled = true;
            return;
        }

        var sources = new HashSet<EntityUid> { used };
        var accessTags = _accessReader.FindAccessTags(used, sources);
        _accessReader.FindStationRecordKeys(used, out var stationKeys, sources);

        if (!_accessReader.IsAllowed(accessTags, stationKeys, uid, accessReader))
        {
            _chat.TrySendInGameICMessage(uid, Loc.GetString("dominator-auth-notallowed"), InGameICChatType.Speak, true);
            return;
        }

        var name = idCard.FullName ?? Loc.GetString("Неизвестный пользователь");
        _chat.TrySendInGameICMessage(uid, Loc.GetString("dominator-auth-success", ("name", name)), InGameICChatType.Speak, true);
        comp.AuthorizedID = used;
        Dirty(uid, comp);
        args.Handled = true;
    }

}
