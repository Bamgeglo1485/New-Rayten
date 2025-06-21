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

        if (comp.AuthorizedID != null)
            return;

        if (!TryComp<IdCardComponent>(used, out var idCard))
            return;

        if (!TryComp<AccessComponent>(used, out var access))
            return;

        if (!TryComp<AccessReaderComponent>(uid, out var accessReader))
            return;

        // ВАЖНО: проверяем доступ не у user, а у used (карты)
        if (!_accessReader.IsAllowed(uid, used, accessReader))
        {
            _chat.TrySendInGameICMessage(uid, Loc.GetString("Несанкционированный доступ."), InGameICChatType.Speak, true);
            return;
        }

        if (idCard != null)
        {
            var name = idCard.FullName ?? Loc.GetString("Неизвестный пользователь");
            _chat.TrySendInGameICMessage(uid, Loc.GetString("Авторизация завершена. Здравствуйте,", ("name", name) ), InGameICChatType.Speak, true);
        }
        else
        {
            _chat.TrySendInGameICMessage(uid, Loc.GetString("Айди авторизовано в системе."), InGameICChatType.Speak, true);
        }
        comp.AuthorizedID = used;
        args.Handled = true;
    }

}
