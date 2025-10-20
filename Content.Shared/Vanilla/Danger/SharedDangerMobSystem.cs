using Content.Shared.PDA;
using Content.Shared.Inventory;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs;
using Content.Shared.Contraband;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.CombatMode;
using Content.Shared.Storage;
using Content.Shared.Roles;
using Content.Shared.Storage.Components;
using Robust.Shared.Prototypes;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Content.Shared.Vanilla.Dominator;

public class SharedDangerMobSystem : EntitySystem
{
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedCombatModeSystem _combat = default!;

    public int GetEntityDanger(EntityUid target, bool deepseek = false)
    {
        if (!TryComp<DangerMobComponent>(target, out var dangerComp))
            return 0;

        if (!TryComp<MobStateComponent>(target, out var mobstate))
            return 0;

        if (mobstate.CurrentState != MobState.Alive)
            return 0;

        if (dangerComp.MaxDanger)
            return 10;

        return deepseek ? CalculateDeepDanger(target, dangerComp) : dangerComp.Danger;
    }

    //считаем глубокую опасность
    protected int CalculateDeepDanger(EntityUid target, DangerMobComponent dangerComp)
    {
        // --- 1. Проверка на опасных существ ---
        if (dangerComp.MaxDanger)
            return 10;

        var deepdanger = 0;
        List<ProtoId<DepartmentPrototype>> departments = new();
        var jobId = "";

        // --- 2. Проверка на карту агента ---
        if (TryGetIdCard(target, out var idcard))
        {
            TryComp<AgentIDCardComponent>(idcard, out var agentCardComp);

            if (TryComp<PdaComponent>(idcard, out var pdaComp))
                TryComp<AgentIDCardComponent>(pdaComp.ContainedId, out agentCardComp);

            // Карта агента скрывает глубокое сканирование
            if (agentCardComp?.HideDeepScan == true)
                return dangerComp.Danger;

            departments = idcard.Comp.JobDepartments;
            if (idcard.Comp.LocalizedJobTitle is not null)
                jobId = idcard.Comp.LocalizedJobTitle;
        }

        // --- 3. Проверка рук ---
        foreach (var item in _hands.EnumerateHeld(target))
            deepdanger += GetRecursiveItemDanger(item, departments, jobId);

        // --- 3. Проверка инвентарных слотов ---
        if (TryComp<InventoryComponent>(target, out var inventoryComp))
        {
            foreach (var slot in inventoryComp.Slots)
            {
                if (_inventory.TryGetSlotEntity(target, slot.Name, out var itemUid) && itemUid is { } itemUidValue)
                    deepdanger += GetRecursiveItemDanger(itemUidValue, departments, jobId);
            }
        }
        return Math.Clamp(deepdanger, 0, 10);
    }
    private int GetRecursiveItemDanger(EntityUid uid, List<ProtoId<DepartmentPrototype>> departments, string jobId)
    {
        var totalDanger = GetItemDanger(uid, departments, jobId);

        // Проверяем, есть ли внутри что-то
        if (TryComp<StorageComponent>(uid, out var storageComp))
        {
            foreach (var contained in storageComp.Container.ContainedEntities)
                totalDanger += GetRecursiveItemDanger(contained, departments, jobId);
        }

        if (TryComp<SecretStashComponent>(uid, out var stashComp) && stashComp.ItemContainer.ContainedEntity.HasValue)
            totalDanger += GetItemDanger(stashComp.ItemContainer.ContainedEntity.Value, departments, jobId);

        return totalDanger;
    }

    //считаем внешнюю опасность
    protected void CalculateDanger(EntityUid target, DangerMobComponent dangercomp)
    {
        // --- 1. Проверка на опасных существ ---
        if (dangercomp.MaxDanger)
            return;

        var danger = 0;
        List<ProtoId<DepartmentPrototype>> departments = [];
        var jobId = "";

        if (TryGetIdCard(target, out var idcard))
        {
            departments = idcard.Comp.JobDepartments;
            if (idcard.Comp.LocalizedJobTitle is not null)
                jobId = idcard.Comp.LocalizedJobTitle;
        }

        // --- 2. Проверка рук ---
        // Предметы в руках имеют в два раза большую опасность, если цель в харммоде
        var harmmodemodifier = _combat.IsInCombatMode(target) ? 2 : 1;
        foreach (var item in _hands.EnumerateHeld(target))
            danger += GetItemDanger(item, departments, jobId) * harmmodemodifier;

        // --- 3. Проверка инвентарных слотов ---
        if (TryComp<InventoryComponent>(target, out var inventoryComp))
        {
            foreach (var slot in inventoryComp.Slots)
            {
                if (_inventory.TryGetSlotEntity(target, slot.Name, out var itemUid) && itemUid is { } itemUidValue)
                {
                    if (slot.Name != "pocket1" && slot.Name != "pocket2")
                        danger += GetItemDanger(itemUidValue, departments, jobId);
                }
            }
        }
        // --- 4. Проверка на харммод ---
        if (_combat.IsInCombatMode(target) && danger > 0)
            danger += 2;

        dangercomp.Danger = Math.Clamp(danger, 0, 10);
    }

    private bool TryGetIdCard(EntityUid target, [NotNullWhen(true)] out Entity<IdCardComponent> idCard)
    {
        IdCardComponent? idCardComp;

        if (TryComp(target, out idCardComp))
        {
            idCard = (target, idCardComp);
            return true;
        }

        if (_inventory.TryGetSlotEntity(target, "id", out var heldId))
        {
            if (TryComp(heldId, out idCardComp))
            {
                idCard = (heldId.Value, idCardComp);
                return true;
            }

            if (TryComp<PdaComponent>(heldId, out var pdaComp) && TryComp(pdaComp.ContainedId, out idCardComp))
            {
                idCard = (pdaComp.ContainedId.Value, idCardComp);
                return true;
            }
        }

        foreach (var item in _hands.EnumerateHeld(target))
        {
            if (TryComp(item, out idCardComp))
            {
                idCard = (item, idCardComp);
                return true;
            }
        }

        idCard = default;
        return false;
    }

    private int GetItemDanger(EntityUid item, List<ProtoId<DepartmentPrototype>> departments, string jobId)
    {
        if (!TryComp<ContrabandComponent>(item, out var contraband))
            return 0;

        if (!_proto.TryIndex(contraband.Severity, out var severityProto))
            return 0;

        var jobs = contraband.AllowedJobs.Select(p => _proto.Index(p).LocalizedName).ToArray();

        if (departments.Intersect(contraband.AllowedDepartments).Any() || jobs.Contains(jobId) || jobId == "капитан")
            return 0;

        return severityProto.Danger;
    }
}
