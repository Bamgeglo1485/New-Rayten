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
using Robust.Shared.Prototypes;
using System.Linq;

namespace Content.Shared.Vanilla.Dominator;

public class SharedDangerMobSystem : EntitySystem
{
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedIdCardSystem _id = default!;

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

        return deepseek ? dangerComp.DeepDanger : dangerComp.Danger;
    }

    protected void CalculateDanger(EntityUid target, DangerMobComponent dangercomp)
    {
        // --- 1. Проверка на опасных существ ---
        if (dangercomp.MaxDanger)
            return;

        List<ProtoId<DepartmentPrototype>> departments = new();
        var jobId = "";
        if (_id.TryFindIdCard(target, out var id))
        {
            departments = id.Comp.JobDepartments;
            if (id.Comp.LocalizedJobTitle is not null)
            {
                jobId = id.Comp.LocalizedJobTitle;
            }
        }

        var danger = 0;
        // --- 2. Проверка рук ---
        // Предметы в руках имеют в два раза большую опасность
        foreach (var item in _hands.EnumerateHeld(target))
        {
            danger += GetItemDanger(item, departments, jobId) * 2;
        }

        // --- 3. Проверка инвентарных слотов ---
        var deepdanger = danger;
        if (TryComp<InventoryComponent>(target, out var inventoryComp))
        {
            foreach (var slot in inventoryComp.Slots)
            {
                if (_inventory.TryGetSlotEntity(target, slot.Name, out var itemUid) && itemUid is { } itemUidValue)
                {
                    // Учитываем опасность самого предмета
                    var itemdanger = GetItemDanger(itemUidValue, departments, jobId);
                    deepdanger += itemdanger;

                    if (slot.Name != "pocket1" && slot.Name != "pocket2")
                        danger += itemdanger;

                    if (TryComp<StorageComponent>(itemUidValue, out var storageComp))
                    {
                        foreach (var contained in storageComp.Container.ContainedEntities)
                        {
                            deepdanger += GetItemDanger(contained, departments, jobId);
                        }
                    }
                }
            }
        }
        // --- 4. Проверка на карту агента ---
        if (_inventory.TryGetSlotEntity(target, "id", out var heldId))
        {
            if (HasComp<AgentIDCardComponent>(heldId))
            {
                danger -= 2;
            }
            else
            {
                if (TryComp<PdaComponent>(heldId, out var pda) && pda.ContainedId.HasValue && HasComp<AgentIDCardComponent>(pda.ContainedId.Value))
                {
                    danger -= 2;
                }
            }
        }
        // --- 2. Проверка на харммод ---
        if (TryComp<CombatModeComponent>(target, out var combat) && combat.IsInCombatMode)
        {
            if (danger>0)
                danger += 2;

            if (deepdanger > 0)
                deepdanger += 2;
        }
        dangercomp.Danger = Math.Clamp(danger, 0, 10);
        dangercomp.DeepDanger = Math.Clamp(deepdanger, 0, 10);
    }

    private int GetItemDanger(EntityUid item, List<ProtoId<DepartmentPrototype>> departments, string jobId)
    {
        if (!TryComp<ContrabandComponent>(item, out var contraband))
            return 0;

        if (!_proto.TryIndex(contraband.Severity, out var severityProto))
            return 0;

        var jobs = contraband.AllowedJobs.Select(p => _proto.Index(p).LocalizedName).ToArray();

        if (departments.Intersect(contraband.AllowedDepartments).Any() || jobs.Contains(jobId))
            return 0;

        return severityProto.Danger;
    }
}
