
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Vanilla.Voices;
using Content.Shared.DoAfter;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Body.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Mobs;
using Content.Shared.Popups;
using Content.Shared.Sprite;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using System.Numerics;

namespace Content.Shared.Vanilla.Entities.BrainWorm;

public partial class BrainWormSystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedActionsSystem _action = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedScaleVisualsSystem _scaleVisuals = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    private static readonly EntProtoId BrainWormShopId = "ActionBrainWormShop";
    private static readonly EntProtoId BrainWormChemicalsId = "ActionBrainWormChemicals";

    private static readonly ReagentId SugarId = new ReagentId("Sugar", null);
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BrainWormComponent, InsertBrainDoAfterEvent>(OnInsertDoAfter);

        SubscribeLocalEvent<BrainWormComponent, EjectBrainDoAfterEvent>(OnEjectDoAfter);
        SubscribeLocalEvent<BrainWormComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<BrainWormComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<BrainWormHostComponent, SolutionContainerChangedEvent>(OnSolutionChanged);
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
        _action.RemoveAction(actions, component.ActionEjectBrainEntity);
        _action.RemoveAction(actions, component.ActionWormShopEntity);
        _action.RemoveAction(actions, component.ActionWormChemicalsEntity);
        _action.RemoveAction(actions, component.ActionMindControlEntity);

        _action.AddAction(uid, ref component.ActionInsertBrainEntity, component.ActionInsertBrain, component: comp);

        // Удаляем приватное общение
        RemComp<PrivateTalkComponent>(uid);
        component.EjectDoAfter = null;
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
        privatetalkcomp.receiver = target;

        if (TryComp(worm, out ActionsComponent? comp))
        {
            var actions = new Entity<ActionsComponent?>(worm, comp);

            _action.AddAction(worm, ref wormcomp.ActionEjectBrainEntity, wormcomp.ActionEjectBrain, component: comp);
            _action.AddAction(worm, ref wormcomp.ActionWormShopEntity, BrainWormShopId, component: comp);
            _action.AddAction(worm, ref wormcomp.ActionWormChemicalsEntity, BrainWormChemicalsId, component: comp);
            _action.AddAction(worm, ref wormcomp.ActionMindControlEntity, wormcomp.ActionMindControl, component: comp);
            _action.RemoveAction(actions, wormcomp.ActionInsertBrainEntity);
        }
        args.Handled = true;
    }

    private void OnSolutionChanged(EntityUid uid, BrainWormHostComponent comp, ref SolutionContainerChangedEvent args)
    {
        if (args.Solution.Name != "chemicals")
            return;

        if (!TryComp<BrainWormComponent>(comp.HostedBrainWorm, out var wormComp))
            return;

        var hasSugar = args.Solution.GetReagentQuantity(SugarId) > 0;

        if (hasSugar && !wormComp.IsSleep)
        {
            _popup.PopupClient(Loc.GetString("brainworm-popup-worm-get-sleep"), comp.HostedBrainWorm, comp.HostedBrainWorm, PopupType.Large);
        }

        wormComp.IsSleep = hasSugar;

        if (hasSugar)
        {
            CancelDoAfter(wormComp);
            var ev = new ReControlEvent();
            RaiseLocalEvent(uid, ref ev);
        }
    }

    private void OnRelayMovement(EntityUid uid, BrainWormComponent wormComp, ref MoveInputEvent args)
    {
        CancelDoAfter(wormComp);
    }

    private void OnHostInit(EntityUid uid, BrainWormHostComponent component, ComponentInit args)
    {
        //инициализируем контейнер
        component.BrainWormContainer = _container.EnsureContainer<ContainerSlot>(uid, "BrainWormContainer");
        component.BrainWormContainer.ShowContents = false;
        component.BrainWormContainer.OccludesLight = true;

        //добавляем рутконтейнер как контейнер для разума, лочим его общение только с основным телом.
        if (!TryComp<BodyComponent>(uid, out var body) || body.RootContainer.ContainedEntity == null)
            return;

        component.MindCage = body.RootContainer.ContainedEntity.Value;
        var privatetalkcomp = EnsureComp<PrivateTalkComponent>(component.MindCage);
        privatetalkcomp.receiver = uid;
    }

    private void OnHostShutdown(EntityUid uid, BrainWormHostComponent component, ComponentShutdown args)
    {
        if (!TryComp(uid, out ActionsComponent? comp))
            return;

        var actions = new Entity<ActionsComponent?>(uid, comp);
        _action.RemoveAction(actions, component.ActionBrainWormReproduceEntity);

        RemComp<PrivateTalkComponent>(component.MindCage);
    }

    private void OnMapInit(EntityUid uid, BrainWormComponent component, MapInitEvent args)
    {
        component.NextChemicalsTime = _timing.CurTime;
        var scale = _scaleVisuals.GetSpriteScale(uid) * 0.5f;
        _scaleVisuals.SetSpriteScale(uid, scale);
        if (!TryComp(uid, out ActionsComponent? comp))
            return;
        _action.AddAction(uid, ref component.ActionInsertBrainEntity, component.ActionInsertBrain, component: comp);
    }

    private void OnShutdown(EntityUid uid, BrainWormComponent component, ComponentShutdown args)
    {
        //выселяем червя
        EjectWorm(uid);

        //удаляем акшен вселения
        if (!TryComp(uid, out ActionsComponent? comp))
            return;
        var actions = new Entity<ActionsComponent?>(uid, comp);
        _action.RemoveAction(actions, component.ActionInsertBrainEntity);
    }

    //Корректно завершает все дуафтеры
    private void CancelDoAfter(BrainWormComponent wormComp)
    {
        _doAfter.Cancel(wormComp.EjectDoAfter);
        _doAfter.Cancel(wormComp.MindControlDoAfter);
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

        if (!_doAfter.TryStartDoAfter(doAfterEventArgs, out hostcomp.ReControlDoAfter))
            return;

        component.IsEscaping = true;
    }
}
