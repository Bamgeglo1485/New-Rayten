
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Vanilla.Voices;
using Content.Shared.DoAfter;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Body.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Sprite;
using Content.Shared.Atmos.Components;
using Content.Shared.Body;
using Robust.Shared.Timing;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Shared.Vanilla.Entities.BrainWorm;

public abstract partial class SharedBrainWormSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] protected SharedActionsSystem Action = default!;
    [Dependency] protected SharedDoAfterSystem DoAfter = default!;
    [Dependency] protected SharedPopupSystem Popup = default!;
    [Dependency] protected SharedScaleVisualsSystem ScaleVisuals = default!;
    [Dependency] private BodySystem _body = default!;
    [Dependency] protected IGameTiming Timing = default!;
    [Dependency] protected MobStateSystem Mob = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private InjectorSystem _injector = default!;

    private static readonly EntProtoId BrainWormShopId = "ActionBrainWormShop";
    private static readonly EntProtoId BrainWormChemicalsId = "ActionBrainWormChemicals";
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BrainWormComponent, InsertBrainDoAfterEvent>(OnInsertDoAfter);

        SubscribeLocalEvent<BrainWormComponent, EjectBrainDoAfterEvent>(OnEjectDoAfter);
        SubscribeLocalEvent<BrainWormComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<BrainWormComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<BrainWormHostComponent, ComponentInit>(OnHostInit);
        SubscribeLocalEvent<BrainWormHostComponent, ComponentShutdown>(OnHostShutdown);

        SubscribeLocalEvent<BrainWormHostComponent, MobStateChangedEvent>(OnHostStateChanged);
        SubscribeLocalEvent<BrainWormComponent, MobStateChangedEvent>(OnWormStateChanged);

        SubscribeLocalEvent<BrainWormComponent, MoveInputEvent>(OnRelayMovement);
        SubscribeLocalEvent<UnderControlComponent, MoveInputEvent>(OnUnderControlRelayMovement);
        SubscribeLocalEvent<BrainWormComponent, BrainWormUpgradeEvent>(OnUpgrade);

        SubscribeLocalEvent<InsertInBrainEvent>(InsertInBrain);
        SubscribeLocalEvent<BrainWormComponent, EjectBrainEvent>(EjectBrain);
        SubscribeLocalEvent<BrainWormComponent, BrainWormForceSayActionEvent>(OnForceSay);
        SubscribeLocalEvent<BrainWormHostComponent, BrainWormReturnControlActionEvent>(OnReturnControl);
        SubscribeLocalEvent<BrainWormComponent, MindControlEvent>(OnMindControl);
        SubscribeLocalEvent<BrainWormComponent, BrainWormChemicalsActionEvent>(OnChemicals);
    }
    private void OnUpgrade(EntityUid uid, BrainWormComponent component, BrainWormUpgradeEvent args)
    {
        switch (args.Upgrade)
        {
            case UpgradeType.inserbrain:
                // уменьшение времени внедрения
                component.InsertDoAfterTime = 2.0f;
                break;

            case UpgradeType.reproduce:
                component.HasReproduceUpgrade = true;
                break;

            case UpgradeType.chemupgrade:
                // апгрейд химсекреции
                component.ChemicalsPerTick += 0.5f;
                component.MindControlDoAfterTime /= 2;
                break;

            default:
                Log.Warning($"BrainWormUpgradeEvent получил неизвестный апгрейд {args.Upgrade}");
                break;
        }
    }

    private void OnWormStateChanged(EntityUid uid, BrainWormComponent component, MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Alive)
            return;

        if (!component.TryGetHost(out var host))
            return;

        var ev = new ReControlEvent();
        RaiseLocalEvent(host, ref ev);
        EjectWorm(uid);
    }

    private void OnHostStateChanged(EntityUid uid, BrainWormHostComponent component, MobStateChangedEvent args)
    {
        var ev = new ReControlEvent();
        RaiseLocalEvent(uid, ref ev);
    }

    private void OnEjectDoAfter(EntityUid uid, BrainWormComponent component, DoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Args.Target == null)
            return;

        if (!component.TryGetHost(out var host))
            return;

        if (component.IsSleep)
            return;

        if (!TryComp<ActionsComponent>(uid, out var comp))
            return;

        if (component.IsMindController)
            return;

        if (component.Currentstage == BrainWormLifeStage.Elder)
            return;

        EjectWorm(uid);
    }

    public void EjectWorm(EntityUid uid)
    {
        if (!TryComp<BrainWormComponent>(uid, out var component))
            return;

        if (!component.TryGetHost(out var host))
            return;

        // Выселяем червя
        if (TryComp<BrainWormHostComponent>(host, out var hostComponent))
        {
            _container.Remove(uid, hostComponent.BrainWormContainer);
            RemComp<BrainWormHostComponent>(host);
        }

        if (!TryComp(uid, out ActionsComponent? comp))
            return;

        // Меняем акшенсы
        var actions = new Entity<ActionsComponent?>(uid, comp);
        Action.RemoveAction(actions, component.ActionEjectBrainEntity);
        Action.RemoveAction(actions, component.ActionWormShopEntity);
        Action.RemoveAction(actions, component.ActionWormChemicalsEntity);
        Action.RemoveAction(actions, component.ActionMindControlEntity);

        Action.AddAction(uid, ref component.ActionInsertBrainEntity, component.ActionInsertBrain, component: comp);

        // Удаляем приватное общение
        RemComp<PrivateTalkComponent>(uid);
        component.EjectDoAfter = null;
        // Убираем резист к поджогу
        EnsureComp<FlammableComponent>(uid);
        component.SetHost(null);
    }

    private void OnInsertDoAfter(EntityUid worm, BrainWormComponent wormcomp, DoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Args.Target == null)
            return;

        if (HasComp<BrainWormHostComponent>(args.Args.Target.Value))
            return;

        var target = args.Args.Target.Value;
        var hostcomp = EnsureComp<BrainWormHostComponent>(target);
        _container.Insert(worm, hostcomp.BrainWormContainer);
        hostcomp.HostedBrainWorm = worm;
        wormcomp.SetHost(target);
        var privatetalkcomp = EnsureComp<PrivateTalkComponent>(worm);
        privatetalkcomp.Receivers.Add(target);
        RemComp<FlammableComponent>(worm);
        if (TryComp(worm, out ActionsComponent? comp))
        {
            var actions = new Entity<ActionsComponent?>(worm, comp);

            Action.AddAction(worm, ref wormcomp.ActionEjectBrainEntity, wormcomp.ActionEjectBrain, component: comp);
            Action.AddAction(worm, ref wormcomp.ActionWormShopEntity, BrainWormShopId, component: comp);
            Action.AddAction(worm, ref wormcomp.ActionWormChemicalsEntity, BrainWormChemicalsId, component: comp);
            Action.AddAction(worm, ref wormcomp.ActionMindControlEntity, wormcomp.ActionMindControl, component: comp);
            Action.RemoveAction(actions, wormcomp.ActionInsertBrainEntity);
        }
        args.Handled = true;
    }



    private void OnRelayMovement(EntityUid uid, BrainWormComponent wormComp, ref MoveInputEvent args)
    {
        CancelDoAfter(wormComp);
    }

    private void OnHostInit(EntityUid uid, BrainWormHostComponent component, ComponentInit args)
    {
        // Инициализируем контейнер червя
        component.BrainWormContainer = _container.EnsureContainer<ContainerSlot>(uid, "BrainWormContainer");
        component.BrainWormContainer.ShowContents = false;
        component.BrainWormContainer.OccludesLight = true;

        // Проверяем, есть ли тело
        if (!TryComp<BodyComponent>(uid, out var body))
            return;

        // Получаем органы с BrainComponent
        if (!_body.TryGetOrgansWithComponent<BrainComponent>((uid, body), out var brains) || brains.Count == 0)
            return;

        component.MindCage = brains[0];

        // Устанавливаем приватное общение с основным телом
        var privatetalkcomp = EnsureComp<PrivateTalkComponent>(component.MindCage);
        privatetalkcomp.Receivers.Add(uid);
    }


    private void OnHostShutdown(EntityUid uid, BrainWormHostComponent component, ComponentShutdown args)
    {
        if (!TryComp(uid, out ActionsComponent? comp))
            return;

        var actions = new Entity<ActionsComponent?>(uid, comp);
        Action.RemoveAction(actions, component.ActionBrainWormReproduceEntity);

        RemComp<PrivateTalkComponent>(component.MindCage);
    }

    private void OnMapInit(EntityUid uid, BrainWormComponent component, MapInitEvent args)
    {
        component.NextChemicalsTime = Timing.CurTime;
        var scale = ScaleVisuals.GetSpriteScale(uid) * 0.5f;
        ScaleVisuals.SetSpriteScale(uid, scale);
        if (!TryComp(uid, out ActionsComponent? comp))
            return;
        Action.AddAction(uid, ref component.ActionInsertBrainEntity, component.ActionInsertBrain, component: comp);
    }

    private void OnShutdown(EntityUid uid, BrainWormComponent component, ComponentShutdown args)
    {
        //выселяем червя
        EjectWorm(uid);

        //удаляем акшен вселения
        if (!TryComp(uid, out ActionsComponent? comp))
            return;
        var actions = new Entity<ActionsComponent?>(uid, comp);
        Action.RemoveAction(actions, component.ActionInsertBrainEntity);
    }

    //Корректно завершает все дуафтеры
    protected void CancelDoAfter(BrainWormComponent wormComp)
    {
        DoAfter.Cancel(wormComp.EjectDoAfter);
        DoAfter.Cancel(wormComp.MindControlDoAfter);
        wormComp.EjectDoAfter = null;
        wormComp.MindControlDoAfter = null;
    }

    private void OnUnderControlRelayMovement(EntityUid uid, UnderControlComponent component, ref MoveInputEvent args)
    {
        if (component.IsEscaping)
            return;

        if (!TryComp<BrainWormHostComponent>(component.OriginalMob, out var hostcomp))
            return;

        var doAfterEventArgs = new DoAfterArgs(EntityManager, component.OriginalMob, component.BaseResistTime, new ReControlDoAfterEvent(), component.OriginalMob)
        {
            Hidden = true
        };

        if (!DoAfter.TryStartDoAfter(doAfterEventArgs, out hostcomp.ReControlDoAfter))
            return;

        component.IsEscaping = true;
    }
}
