using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged;
using Content.Shared.PDA;
using Content.Shared.Inventory;
using Content.Shared.Examine;
using Content.Shared.Access.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Hands.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared.Vanilla.Dominator;

public class SharedDominatorSystem : EntitySystem
{
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly DangerMobSystem _dangermob = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearanceSystem = default!;
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

            if (!_hands.TryGetActiveItem(parent, out var helditem))
                continue;

            if (helditem != uid)
                continue;

            var newMode = GetFireMode(uid, dom, parent);

            if (newMode != dom.CurrentState)
            {
                UpdateWeaponMode(uid, dom, newMode);
            }
        }
    }

    private DominatorState GetFireMode(EntityUid uid, DominatorComponent dom, EntityUid gunuser)
    {
        var ents = _lookup.GetEntitiesInRange(uid, dom.ScanRange, LookupFlags.Dynamic | LookupFlags.Approximate);

        int maxdanger = 0;

        foreach (var target in ents)
        {
            if (target == uid || target == gunuser)
                continue;

            //если цель за стеной - игнорируем
            if (!_examine.InRangeUnOccluded(uid, target, 10f, ignoreInsideBlocker: false))
                continue;

            //считаем опасность цели
            int targetdanger = _dangermob.GetEntityDanger(target, false);

            if (targetdanger > maxdanger)
                maxdanger = targetdanger;
        }

        return maxdanger switch
        {
            >= 10 => DominatorState.Lethal,
            >= 4 => DominatorState.NonLethal,
            _ => DominatorState.Disabled
        };
    }

    public virtual void UpdateWeaponMode(EntityUid uid, DominatorComponent component, DominatorState newMode)
    {
        component.CurrentState = newMode;
        // Dirty(uid, component);

        var fireMode = component.FireModes[(int)newMode];
        if (fireMode.IsHitscan)
        {

            if (_proto.TryIndex<HitscanPrototype>(fireMode.Prototype, out var prototype))
            {
                if (TryComp<AppearanceComponent>(uid, out var appearance))
                {
                    _appearanceSystem.SetData(uid, BatteryWeaponFireModeVisuals.State, prototype.ID, appearance);
                }
            }

            if (TryComp(uid, out HitscanBatteryAmmoProviderComponent? hitscanBatteryAmmoProviderComponent))
            {
                // TODO: Have this get the info directly from the batteryComponent when power is moved to shared.
                var oldFireCost = hitscanBatteryAmmoProviderComponent.FireCost;
                hitscanBatteryAmmoProviderComponent.Prototype = fireMode.Prototype;
                hitscanBatteryAmmoProviderComponent.FireCost = fireMode.FireCost;

                float fireCostDiff = (float)fireMode.FireCost / (float)oldFireCost;
                hitscanBatteryAmmoProviderComponent.Shots = (int)Math.Round(hitscanBatteryAmmoProviderComponent.Shots / fireCostDiff);
                hitscanBatteryAmmoProviderComponent.Capacity = (int)Math.Round(hitscanBatteryAmmoProviderComponent.Capacity / fireCostDiff);

                Dirty(uid, hitscanBatteryAmmoProviderComponent);

                var updateClientAmmoEvent = new UpdateClientAmmoEvent();
                RaiseLocalEvent(uid, ref updateClientAmmoEvent);
            }
        }
        else
        {
            if (_proto.TryIndex<EntityPrototype>(fireMode.Prototype, out var prototype))
            {
                if (TryComp<AppearanceComponent>(uid, out var appearance))
                {
                    _appearanceSystem.SetData(uid, BatteryWeaponFireModeVisuals.State, prototype.ID, appearance);
                }
            }

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
