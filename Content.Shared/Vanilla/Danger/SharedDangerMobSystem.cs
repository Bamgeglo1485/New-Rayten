using Content.Shared.Vanilla.Entities.DangerScanner;
using Content.Shared.Access.Components;
using Content.Shared.PDA;
using Content.Shared.Inventory;
using Content.Shared.Access.Systems;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs;
using Content.Shared.Contraband;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.CombatMode;
using Content.Shared.Storage;
using Content.Shared.Roles;
using Content.Shared.Storage.Components;
using Content.Shared.Security.Components;
using Content.Shared.Security;
using Robust.Shared.Prototypes;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Content.Shared.Vanilla.Dominator;

public abstract partial class SharedDangerMobSystem : EntitySystem
{
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SharedCombatModeSystem _combat = default!;

    #region API
    public bool TryGetDangeriousItem(EntityUid target, [NotNullWhen(true)] out EntityUid? mostDangerousItem)
    {
        mostDangerousItem = null;

        if (!TryComp<DangerMobComponent>(target, out var dangerComp))
            return false;

        if (dangerComp.MaxDanger)
            return false;

        return CalculateDeepDanger(target, dangerComp, out mostDangerousItem) > 0;
    }

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

        var minDanger = 0;

        if (TryComp<CriminalRecordComponent>(target, out var record))
        {
            minDanger = record.Status switch
            {
                SecurityStatus.Wanted => 6, // в розыске
                SecurityStatus.Detained => 6, // под арестом
                SecurityStatus.Hostile => 10, // враг НТ
                SecurityStatus.Eliminated => 10, // Ликвидирован
                _ => 0
            };
        }

        var danger = deepseek ? CalculateDeepDanger(target, dangerComp, out var mostDangerousItem) : dangerComp.Danger;

        if (minDanger > danger)
            danger = minDanger;

        return Math.Clamp(danger, 0, 10);
    }
    public int GetItemDanger(EntityUid item, List<ProtoId<DepartmentPrototype>> departments, string jobId)
    {
        if (!TryComp<ContrabandComponent>(item, out var contraband))
            return 0;

        if (!_proto.TryIndex(contraband.Severity, out var severityProto))
            return 0;

        var jobs = contraband.AllowedJobs.Select(p => _proto.Index(p).LocalizedName).ToArray();

        if (departments.Intersect(contraband.AllowedDepartments).Any()
            || jobs.Contains(jobId)
            || ((contraband.AllowedDepartments.Count > 0 || contraband.AllowedJobs.Count > 0) && jobId == "капитан"))
            return 0;

        return severityProto.Danger;
    }
    #endregion
    #region глубокая опасность
    protected int CalculateDeepDanger(EntityUid target, DangerMobComponent dangerComp, out EntityUid? mostDangerousItem)
    {
        mostDangerousItem = null;

        if (dangerComp.MaxDanger)
            return 10;

        var deepdanger = 0;
        int globalMaxDanger = 0;

        List<ProtoId<DepartmentPrototype>> departments = new();
        var jobId = "";

        // ID card
        if (TryGetIdCard(target, out var idcard))
        {
            departments = idcard.Comp.JobDepartments;
            if (idcard.Comp.LocalizedJobTitle is not null)
                jobId = idcard.Comp.LocalizedJobTitle;
        }

        // Hands
        foreach (var held in _hands.EnumerateHeld(target))
        {
            deepdanger += GetRecursiveItemDanger(held, departments, jobId, out var childItem);

            var d = GetItemDanger(childItem, departments, jobId);
            if (d > globalMaxDanger)
            {
                globalMaxDanger = d;
                mostDangerousItem = childItem;
            }
        }

        // Inventory slots
        if (TryComp<InventoryComponent>(target, out var inventoryComp))
        {
            foreach (var slot in inventoryComp.Slots)
            {
                if (!_inventory.TryGetSlotEntity(target, slot.Name, out var itemUid))
                    continue;

                deepdanger += GetRecursiveItemDanger(itemUid.Value, departments, jobId, out var childItem);

                var d = GetItemDanger(childItem, departments, jobId);
                if (d > globalMaxDanger)
                {
                    globalMaxDanger = d;
                    mostDangerousItem = childItem;
                }
            }
        }

        if (deepdanger == 0)
            mostDangerousItem = null;

        return deepdanger;
    }

    /// Возвращает полную угрозу предмета с учётом всех предметов внутри него
    /// item - самый опасный предмет, либо сам предмет либо предмет внутри него
    private int GetRecursiveItemDanger(
        EntityUid uid,
        List<ProtoId<DepartmentPrototype>> departments,
        string jobId,
        out EntityUid item)
    {
        EntityUid localMaxItem = uid;
        int localMaxDanger = GetItemDanger(uid, departments, jobId);
        int totalDanger = localMaxDanger;
        if (HasComp<HideContrabandComponent>(uid))
        {
            item = localMaxItem;
            return totalDanger;
        }

        if (TryComp<StorageComponent>(uid, out var storageComp))
        {
            foreach (var contained in storageComp.Container.ContainedEntities)
            {
                totalDanger += GetRecursiveItemDanger(contained, departments, jobId, out var childMaxItem);

                var childDanger = GetItemDanger(childMaxItem, departments, jobId);
                if (childDanger > localMaxDanger)
                {
                    localMaxDanger = childDanger;
                    localMaxItem = childMaxItem;
                }
            }
        }

        if (TryComp<SecretStashComponent>(uid, out var stashComp)
            && stashComp.ItemContainer.ContainedEntity is { } stashedItem)
        {
            var curDanger = GetItemDanger(stashedItem, departments, jobId);
            totalDanger += curDanger;

            if (curDanger > localMaxDanger)
                localMaxItem = stashedItem;
        }

        item = localMaxItem;
        return totalDanger;
    }

    #endregion
    #region внешняя опасность
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
    #endregion
    private bool TryGetIdCard(EntityUid target, [NotNullWhen(true)] out Entity<IdCardComponent> idCard)
    {
        IdCardComponent? idCardComp;
        PdaComponent? pdaComp;
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

            if (TryComp(heldId, out pdaComp) && TryComp(pdaComp.ContainedId, out idCardComp))
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

            if (TryComp(item, out pdaComp) && TryComp(pdaComp.ContainedId, out idCardComp))
            {
                idCard = (pdaComp.ContainedId.Value, idCardComp);
                return true;
            }
        }

        idCard = default;
        return false;
    }
}
