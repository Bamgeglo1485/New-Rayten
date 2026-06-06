using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged;
using Content.Shared.Access.Components;
using Content.Shared.PDA;
using Content.Shared.Inventory;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Hands.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared.Vanilla.Dominator;

public abstract partial class SharedDominatorSystem : EntitySystem
{
    [Dependency] protected InventorySystem _inventory = default!;
    [Dependency] private ExamineSystemShared _examine = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] protected SharedDangerMobSystem _dangermob = default!;
    [Dependency] private SharedAppearanceSystem _appearanceSystem = default!;
    [Dependency] private SharedGunSystem _gun = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DominatorComponent, AttemptShootEvent>(OnAttemptShoot);
        SubscribeLocalEvent<DominatorComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<DangerMobComponent, PreventCollideEvent>(PreventCollide);
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
                UpdateWeaponMode(uid, dom, newMode);
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
            int targetdanger = _dangermob.GetEntityDanger(target);

            if (targetdanger > maxdanger)
                maxdanger = targetdanger;
        }

        return maxdanger switch
        {
            >= 10 => DominatorState.Lethal,
            >= 6 => DominatorState.NonLethal,
            _ => DominatorState.Disabled
        };
    }

    protected virtual void UpdateWeaponMode(EntityUid uid, DominatorComponent component, DominatorState newMode)
    {
        component.CurrentState = newMode;

        var fireMode = component.FireModes[(int)newMode];
        Dirty(uid, component);

        if (_proto.TryIndex<EntityPrototype>(fireMode.Prototype, out var prototype))
        {
            if (TryComp<AppearanceComponent>(uid, out var appearance))
                _appearanceSystem.SetData(uid, BatteryWeaponFireModeVisuals.State, prototype.ID, appearance);
        }

        if (TryComp(uid, out BatteryAmmoProviderComponent? batteryAmmoProviderComponent))
        {
            batteryAmmoProviderComponent.Prototype = fireMode.Prototype;
            batteryAmmoProviderComponent.FireCost = fireMode.FireCost;

            Dirty(uid, batteryAmmoProviderComponent);

            _gun.UpdateShots((uid, batteryAmmoProviderComponent));
        }
    }
    //туду переделать как в системе dangermobsystem
    private void OnAttemptShoot(EntityUid uid, DominatorComponent comp, ref AttemptShootEvent args)
    {
        var user = args.User;

        if (comp.AuthorizedID == null || !Exists(comp.AuthorizedID.Value))
        {
            args.Message = "Оружие не авторизовано.";
            args.Cancelled = true;
            return;
        }

        if (!_inventory.TryGetSlotEntity(user, "id", out var heldId))
        {
            args.Message = "Авторизована другая айди карта";
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
                args.Message = "Авторизована другая айди карта";
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

    private void PreventCollide(EntityUid uid, DangerMobComponent component, ref PreventCollideEvent args)
    {
        if (args.Cancelled)
            return;

        if (TryComp<DangerMobColliderComponent>(args.OtherEntity, out var dangerMobCollider))
        {
            var targetdanger = _dangermob.GetEntityDanger(uid);
            if (targetdanger < dangerMobCollider.MinDanger)
                args.Cancelled = true;
        }

    }
}
