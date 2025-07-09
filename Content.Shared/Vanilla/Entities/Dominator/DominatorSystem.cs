using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.PDA;
using Content.Shared.Inventory;
using Content.Shared.Examine;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Contraband;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Hands.Components;
using Content.Shared.Access.Components;
using Content.Shared.CombatMode;
using Content.Shared.Storage;
using Content.Shared.Roles;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;
using Robust.Shared.Timing;
using System.Linq;

namespace Content.Shared.Vanilla.Dominator;

public class SharedDominatorSystem : EntitySystem
{
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedIdCardSystem _id = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DominatorComponent, AttemptShootEvent>(OnAttemptShoot);
        SubscribeLocalEvent<DominatorComponent, ExaminedEvent>(OnExamined);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<DominatorComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var dom, out var xform))
        {
            dom.Timer += frameTime;

            if (dom.Timer < dom.CheckDelay)
                continue;

            dom.Timer = 0;

            var parent = xform.ParentUid;

            if (!TryComp<HandsComponent>(parent, out var hands))
                continue;

            if (!_hands.TryGetActiveItem(parent, out var helditem))
                continue;

            if (helditem != uid)
                continue;

            var newMode = GetFireMode(uid, dom, xform, parent);

            if (newMode != dom.CurrentState)
            {
                UpdateWeaponMode(uid, dom, newMode);
            }
        }
    }

    private DominatorState GetFireMode(EntityUid uid, DominatorComponent dom, TransformComponent xform, EntityUid gunuser)
    {
        var ents = _lookup.GetEntitiesInRange(uid, dom.ScanRange, LookupFlags.Dynamic | LookupFlags.Approximate);

        int maxdanger = 0;

        foreach (var target in ents)
        {
            if (target == uid || !TryComp<MobStateComponent>(target, out var mobstate) || target == gunuser)
                continue;

            //если цель критованая или мертвая - игнорируем
            if (mobstate.CurrentState != MobState.Alive)
                continue;

            //если цель за стеной - игнорируем
            if (!_examine.InRangeUnOccluded(uid, target, 10f, ignoreInsideBlocker: false))
                continue;

            //считаем опасность цели
            int targetdanger = CalculateTargetDanger(target, false);

            if (targetdanger > maxdanger)
                maxdanger = targetdanger;
        }

        if (maxdanger >= 10)
            return DominatorState.Lethal;

        if (maxdanger >= 4)
            return DominatorState.NonLethal;

        return DominatorState.Disabled;
    }


    /// ВОТ ЭТО ВСЕ В ОТДЕЛЬНУЮ СИСТЕМУ
    private int CalculateTargetDanger(EntityUid target, bool deepseek)
    {
        var danger = 0;

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

        // --- 1. Проверка на опасных существ ---
        if (TryComp<DangerMobComponent>(target, out var dangermob))
        {
            danger += dangermob.Danger;
        }

        // --- 2. Проверка на харммод ---
        if (TryComp<CombatModeComponent>(target, out var combat) && combat.IsInCombatMode)
        {
            danger += 2;
        }
        // --- 2. Проверка рук ---
        // Предметы в руках имеют в два раза большую опасность
        foreach (var item in _hands.EnumerateHeld(target))
        {
            danger += GetItemDanger(item, departments, jobId) * 2;
        }

        // --- 3. Проверка инвентарных слотов ---
        if (TryComp<InventoryComponent>(target, out var inventoryComp))
        {
            foreach (var slot in inventoryComp.Slots)
            {
                // Если не deepseek — пропускаем карманы
                if (!deepseek && (slot.Name == "pocket1" || slot.Name == "pocket2"))
                    continue;

                if (_inventory.TryGetSlotEntity(target, slot.Name, out var itemUid) && itemUid is { } itemUidValue)
                {
                    // Учитываем опасность самого предмета
                    danger += GetItemDanger(itemUidValue, departments, jobId);

                    // Если включён deepseek, проверяем содержимое
                    if (deepseek && TryComp<StorageComponent>(itemUidValue, out var storageComp))
                    {
                        foreach (var contained in storageComp.Container.ContainedEntities)
                        {
                            danger += GetItemDanger(contained, departments, jobId);
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

        return Math.Clamp(danger, 0, 10);
    }


    private int GetItemDanger(EntityUid item, List<ProtoId<DepartmentPrototype>> departments, string jobId)
    {
        if (!TryComp<ContrabandComponent>(item, out var contraband))
            return 0;

        if (!_proto.TryIndex<ContrabandSeverityPrototype>(contraband.Severity, out var severityProto))
            return 0;

        var jobs = contraband.AllowedJobs.Select(p => _proto.Index(p).LocalizedName).ToArray();

        if (departments.Intersect(contraband.AllowedDepartments).Any() || jobs.Contains(jobId))
            return 0;

        return severityProto.Danger;
    }
    /// ВОТ ДО СЮДА

    public virtual void UpdateWeaponMode(EntityUid uid, DominatorComponent component, DominatorState newMode)
    {
        component.CurrentState = newMode;
        Dirty(uid, component);

        var fireMode = component.FireModes[(int)newMode];

        if (_proto.TryIndex<EntityPrototype>(fireMode.Prototype, out var prototype))
        {
            // if (TryComp<AppearanceComponent>(uid, out var appearance))
            //     _appearanceSystem.SetData(uid, BatteryWeaponFireModeVisuals.State, prototype.ID, appearance);
        }

        if (fireMode.IsHitscan)
        {
            if (TryComp(uid, out HitscanBatteryAmmoProviderComponent? hitscanBatteryAmmoProviderComponent))
            {
                // TODO: Have this get the info directly from the batteryComponent when power is moved to shared.
                var OldFireCost = hitscanBatteryAmmoProviderComponent.FireCost;
                hitscanBatteryAmmoProviderComponent.Prototype = fireMode.Prototype;
                hitscanBatteryAmmoProviderComponent.FireCost = fireMode.FireCost;

                float FireCostDiff = (float)fireMode.FireCost / (float)OldFireCost;
                hitscanBatteryAmmoProviderComponent.Shots = (int)Math.Round(hitscanBatteryAmmoProviderComponent.Shots / FireCostDiff);
                hitscanBatteryAmmoProviderComponent.Capacity = (int)Math.Round(hitscanBatteryAmmoProviderComponent.Capacity / FireCostDiff);

                Dirty(uid, hitscanBatteryAmmoProviderComponent);

                var updateClientAmmoEvent = new UpdateClientAmmoEvent();
                RaiseLocalEvent(uid, ref updateClientAmmoEvent);
            }
        }
        else
        {
            if (TryComp(uid, out ProjectileBatteryAmmoProviderComponent? projectileBatteryAmmoProviderComponent))
            {
                // TODO: Have this get the info directly from the batteryComponent when power is moved to shared.
                var OldFireCost = projectileBatteryAmmoProviderComponent.FireCost;
                projectileBatteryAmmoProviderComponent.Prototype = fireMode.Prototype;
                projectileBatteryAmmoProviderComponent.FireCost = fireMode.FireCost;

                float FireCostDiff = (float)fireMode.FireCost / (float)OldFireCost;
                projectileBatteryAmmoProviderComponent.Shots = (int)Math.Round(projectileBatteryAmmoProviderComponent.Shots / FireCostDiff);
                projectileBatteryAmmoProviderComponent.Capacity = (int)Math.Round(projectileBatteryAmmoProviderComponent.Capacity / FireCostDiff);

                Dirty(uid, projectileBatteryAmmoProviderComponent);

                var updateClientAmmoEvent = new UpdateClientAmmoEvent();
                RaiseLocalEvent(uid, ref updateClientAmmoEvent);
            }
        }

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

        if (heldId != comp.AuthorizedID)
        {
            // Проверяем, не содержится ли нужная ID в КПК
            if (!TryComp<PdaComponent>(heldId, out var pda) ||
                !pda.ContainedId.HasValue ||
                pda.ContainedId.Value != comp.AuthorizedID)
            {
                args.Message = "Вы не авторизованы для использования доминатора.";
                args.Cancelled = true;
                return;
            }
        }
        //Проверка на режим стрельбы
        if (comp.CurrentState == DominatorState.Disabled)
        {
            args.Message = "Недопустимая цель. Спусковой механизм заблокирован";
            args.Cancelled = true;
            return;
        }
    }

    private void OnExamined(EntityUid uid, DominatorComponent comp, ExaminedEvent args)
    {
        if (comp.AuthorizedID != null && TryComp<IdCardComponent>(comp.AuthorizedID, out var id))
        {
            var name = id.FullName ?? "Неизвестный пользователь";
            args.PushMarkup(Loc.GetString("dominator-auth-examine-auth", ("name", name)));
        }
        else
        {
            args.PushMarkup(Loc.GetString("dominator-auth-examine-notauth"));
        }
    }
}
