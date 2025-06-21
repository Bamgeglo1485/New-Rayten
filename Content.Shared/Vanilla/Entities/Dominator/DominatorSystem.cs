using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.PDA;
using Content.Shared.Inventory;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Shared.Vanilla.Dominator;

public class SharedDominatorSystem : EntitySystem
{
    [Dependency] private readonly InventorySystem _inventory = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DominatorComponent, AttemptShootEvent>(OnAttemptShoot);
    }
    private void OnAttemptShoot(EntityUid uid, DominatorComponent comp, ref AttemptShootEvent args)
    {
        var user = args.User;

        if (comp.AuthorizedID == null || !EntityManager.EntityExists(comp.AuthorizedID.Value))
        {
            args.Message = "Оружие не авторизовано.";
            args.Cancelled = true;
            return;
        }

        if (!_inventory.TryGetSlotEntity(user, "id", out var heldId))
        {
            args.Message = "Вы не авторизованы для использования доминатора.";
            args.Cancelled = true;
            return;
        }

        // Проверка, является ли это напрямую ID-картой
        if (heldId == comp.AuthorizedID)
            return;

        // Проверка, если это КПК с вставленной ID
        if (TryComp<PdaComponent>(heldId, out var pda)
            && pda.ContainedId.HasValue
            && pda.ContainedId.Value == comp.AuthorizedID)
        {
            return;
        }

        // В остальных случаях - отказ
        args.Message = "Вы не авторизованы для использования доминатора.";
        args.Cancelled = true;
    }

}
