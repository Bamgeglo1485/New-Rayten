using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Vanilla.Entities.BrainWorm;
using Content.Shared.FixedPoint;
using Content.Shared.Store.Components;
using Content.Shared.DoAfter;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Events;
using Content.Shared.Mindshield.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Sprite;
using Content.Shared.Popups;
using Content.Server.Popups;
using Content.Server.Mind;
using Content.Server.Store.Systems;
using Content.Server.Medical;
using Content.Server.Chemistry.Containers.EntitySystems;
using Content.Server.Chat.Systems;
using Robust.Shared.Timing;
using Robust.Shared.Prototypes;
using Robust.Server.GameObjects;
using Robust.Shared.Player;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Random;
using System.Numerics;
using System;

namespace Content.Server.Vanilla.Entities.BrainWorm;

public sealed class ServerBrainWormSystem : EntitySystem
{
    [Dependency] private readonly StoreSystem _store = default!;
    [Dependency] private readonly VomitSystem _vomit = default!;
    [Dependency] private readonly MindSystem _mindSystem = default!;
    [Dependency] private readonly MetaDataSystem _metaSystem = default!;
    [Dependency] private readonly SharedActionsSystem _action = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MobStateSystem _mob = default!;
    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly SolutionContainerSystem _solutions = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedScaleVisualsSystem _scaleVisuals = default!;
    [Dependency] private readonly MobThresholdSystem _mobthreshold = default!;
    [Dependency] private readonly PopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        //открываем магазин эволюций
        SubscribeLocalEvent<BrainWormComponent, BrainWormShopActionEvent>(OnShop);
        //червь создает свое потомство
        SubscribeLocalEvent<BrainWormHostComponent, BrainWormReproduceEvent>(OnReproduce);
        //захватываем контроль над носителем
        SubscribeLocalEvent<BrainWormComponent, MindControlDoAfterEvent>(OnMindControlDoAfter);
        //носитель захотел вернуть себе контроль
        SubscribeLocalEvent<BrainWormHostComponent, ReControlDoAfterEvent>(OnReturnControlDoAfter);
        //Возвращаем червя в червя, носителя в носителя
        SubscribeLocalEvent<BrainWormHostComponent, ReControlEvent>(OnReturnControlEvent);
        //Впрыскиваем химикат
        SubscribeLocalEvent<BrainWormComponent, ChemicalSelectMessage>(OnChemInjection);
        //Заставляем сказать
        SubscribeLocalEvent<BrainWormComponent, ForceSayMessage>(OnForceSay);
    }
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<BrainWormComponent>();
        while (query.MoveNext(out var uid, out var wormcomp))
        {
            if (_timing.CurTime < wormcomp.NextChemicalsTime)
                continue;

            wormcomp.NextChemicalsTime += TimeSpan.FromSeconds(wormcomp.ChemicalsTime);

            if (!wormcomp.TryGetHost(out var host))
                return;

            if (wormcomp.IsSleep)
                continue;

            if (wormcomp.IsMindController)
                continue;

            Dirty(uid, wormcomp);
            wormcomp.Chemicals = Math.Clamp(wormcomp.Chemicals + wormcomp.ChemicalsPerTick, 0, wormcomp.ChemicalsCup);
            _uiSystem.ServerSendUiMessage(uid, BrainWormComponent.ChemicalsUiKey.Key, new ChemicalsupdateMessage(wormcomp.Chemicals, wormcomp.ChemicalsCup));

            if (wormcomp.Currentstage != BrainWormLifeStage.Elder)
                continue;

            if (_mob.IsDead(host))
                continue;

            // Лечим
            if (TryComp<DamageableComponent>(host, out var damage))
                _damageable.TryChangeDamage(host, wormcomp.Heal, true, false, damage);
        }
    }

    private void OnForceSay(EntityUid uid, BrainWormComponent component, ForceSayMessage args)
    {
        if (!component.TryGetHost(out var host))
            return;

        if (_mob.IsIncapacitated(host))
            return;

        if (!BuyAction(component, 30f))
        {
            _popup.PopupEntity(Loc.GetString("brainworm-popup-no-chemiclas", ("chems", 30)), uid, uid, PopupType.Medium);
        }

        _chat.TrySendInGameICMessage(host, args.Text, InGameICChatType.Speak, true);
    }

    private void OnChemInjection(EntityUid uid, BrainWormComponent component, ChemicalSelectMessage args)
    {
        if (!component.Reagents.TryGetValue(args.Selected, out var cost))
            return;

        if (!_prototype.HasIndex<ReagentPrototype>(args.Selected))
            return;

        if (!component.TryGetHost(out var host))
            return;

        if (!TryComp<SolutionContainerManagerComponent>(host, out var solMan))
            return;

        // Получаем solution через систему
        if (!_solutions.TryGetSolution((host, solMan), "chemicals", out var solution))
            return;

        if (!BuyAction(component, cost))
        {
            _popup.PopupEntity(Loc.GetString("brainworm-popup-no-chemiclas", ("chems", cost)), uid, uid, PopupType.Medium);
        }

        var sound = new SoundPathSpecifier("/Audio/Items/hypospray.ogg");
        var filter = Filter.Empty();
        if (TryComp<ActorComponent>(uid, out var wormActor))
            filter.AddPlayer(wormActor.PlayerSession);
        if (TryComp<ActorComponent>(host, out var hostActor))
            filter.AddPlayer(hostActor.PlayerSession);

        _audio.PlayEntity(sound, filter, uid, recordReplay: false);

        _solutions.TryAddReagent(solution.Value, args.Selected, FixedPoint2.New(10), out _);

        Dirty(uid, component);
    }


    private void OnReturnControlEvent(EntityUid uid, BrainWormHostComponent component, ReControlEvent args)
    {
        if (!component.MindUnderControl)
            return;

        _doAfter.Cancel(component.ReControlDoAfter);
        component.ReControlDoAfter = null;

        // Переселяем червя в червя
        if (_mindSystem.TryGetMind(uid, out _, out var wormMind) && wormMind.UserId != null)
        {
            _mindSystem.ControlMob(wormMind.UserId.Value, component.HostedBrainWorm);
            if (TryComp<BrainWormComponent>(component.HostedBrainWorm, out var wormcomp))
                wormcomp.IsMindController = false;
        }

        // Переселяем хозяина в его персонажа
        if (_mindSystem.TryGetMind(component.MindCage, out _, out var hostMind) && hostMind.UserId != null)
            _mindSystem.ControlMob(hostMind.UserId.Value, uid);

        RemComp<UnderControlComponent>(component.MindCage);
        component.MindUnderControl = false;
        //удаляем акшены доступные только в форме контроля носителя
        if (TryComp(uid, out ActionsComponent? comp))
        {
            var actions = new Entity<ActionsComponent?>(uid, comp);
            _action.RemoveAction(actions, component.ActionBrainWormReproduceEntity);
            _action.RemoveAction(actions, component.ActionBrainWormReturnControlEntity);
        }
        Dirty(uid, component);
    }

    private void OnReturnControlDoAfter(EntityUid uid, BrainWormHostComponent component, DoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;
        var ev = new ReControlEvent();
        RaiseLocalEvent(uid, ref ev);
        args.Handled = true;
    }

    private void OnMindControlDoAfter(EntityUid uid, BrainWormComponent component, DoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Args.Target == null)
            return;

        if (!component.TryGetHost(out var host))
            return;

        if (component.IsSleep)
            return;

        if (HasComp<MindShieldComponent>(host))
            return;

        if (!_mob.IsAlive(host))
            return;

        var worm = uid;

        if (!TryComp<BrainWormHostComponent>(host, out var hostcomp))
            return;

        //переселяем носителя в его тело
        if (_mindSystem.TryGetMind(host, out _, out var hostMind) && hostMind.UserId != null)
        {
            _mindSystem.ControlMob(hostMind.UserId.Value, hostcomp.MindCage);
            var undercontrolcomp = EnsureComp<UnderControlComponent>(hostcomp.MindCage);
            undercontrolcomp.OriginalMob = host;
            if (TryComp<MetaDataComponent>(host, out var metaData))
                _metaSystem.SetEntityName(hostcomp.MindCage, metaData.EntityName);
        }

        //переселяем червя в носителя
        if (_mindSystem.TryGetMind(worm, out _, out var wormMind) && wormMind.UserId != null)
        {
            _mindSystem.ControlMob(wormMind.UserId.Value, host);

            component.IsMindController = true;

            if (TryComp(host, out ActionsComponent? comp))
            {
                _action.AddAction(host, ref hostcomp.ActionBrainWormReproduceEntity, hostcomp.ActionBrainWormReproduce, component: comp);
                _action.AddAction(host, ref hostcomp.ActionBrainWormReturnControlEntity, hostcomp.ActionBrainWormReturnControl, component: comp);
            }
        }

        hostcomp.MindUnderControl = true;
        args.Handled = true;
        Dirty(host, hostcomp);
    }

    private void OnShop(EntityUid uid, BrainWormComponent component, BrainWormShopActionEvent args)
    {
        if (!TryComp<StoreComponent>(uid, out var store))
            return;
        _store.ToggleUi(uid, uid, store);
    }

    private void OnReproduce(EntityUid uid, BrainWormHostComponent component, BrainWormReproduceEvent args)
    {
        if (!TryComp<BrainWormComponent>(component.HostedBrainWorm, out var wormcomp))
            return;

        var cost = 100f;
        if (wormcomp.HasReproduceUpgrade)
            cost *= 0.7f;

        if (!BuyAction(wormcomp, cost))
        {
            _popup.PopupEntity(Loc.GetString("brainworm-popup-no-chemiclas", ("chems", cost)), uid, uid, PopupType.Medium);
        }

        _store.TryAddCurrency(
            new Dictionary<string, FixedPoint2>
            {
                { wormcomp.EvolutionPointsPrototype, FixedPoint2.New(1) }
            },
            component.HostedBrainWorm
        );

        _vomit.Vomit(uid);

        var coords = Transform(uid).Coordinates;
        var count = 1;

        if (wormcomp.HasReproduceUpgrade)
        {
            var roll = _random.NextFloat();
            if (roll < 0.05f)
                count = 3;
            else if (roll < 0.15f)
                count = 2;
        }

        for (var i = 0; i < count; i++)
        {
            Spawn("MobBrainWorm", coords);
        }
        wormcomp.Reproducecount++;
        UpgradeLifeStage(component.HostedBrainWorm, wormcomp);
        args.Handled = true;
    }

    private void UpgradeLifeStage(EntityUid uid, BrainWormComponent wormcomp)
    {
        if (wormcomp.Reproducecount >= 3 && wormcomp.Currentstage < BrainWormLifeStage.Mature)
        {
            var scale = _scaleVisuals.GetSpriteScale(uid) * 2f;
            _scaleVisuals.SetSpriteScale(uid, scale);

            wormcomp.Currentstage = BrainWormLifeStage.Mature;
            ApplyUpgrades(uid, 15, -0.1f);
            return;
        }

        if (wormcomp.Reproducecount > 9 && wormcomp.Currentstage < BrainWormLifeStage.Adult)
        {
            wormcomp.ChemicalsPerTick += 0.2f;
            wormcomp.ChemicalsCup += 20f;

            wormcomp.Currentstage = BrainWormLifeStage.Adult;
            ApplyUpgrades(uid, 25, -0.2f);
            return;
        }

        if (wormcomp.Reproducecount > 19 && wormcomp.Currentstage < BrainWormLifeStage.Elder)
        {
            var scale = _scaleVisuals.GetSpriteScale(uid) * 2f;
            _scaleVisuals.SetSpriteScale(uid, scale);

            wormcomp.ChemicalsPerTick += 0.3f;
            wormcomp.ChemicalsCup += 30f;
            wormcomp.Currentstage = BrainWormLifeStage.Elder;
            ApplyUpgrades(uid, 35, -0.3f);
            return;
        }
    }

    private void ApplyUpgrades(EntityUid uid, int newHp, float regen)
    {
        _mobthreshold.SetMobStateThreshold(uid, newHp, MobState.Dead);

        if (TryComp<PassiveDamageComponent>(uid, out var passive))
        {
            passive.Damage.DamageDict["Brute"] = regen;
            passive.Damage.DamageDict["Burn"] = regen;
            passive.Damage.DamageDict["Toxin"] = regen;
            passive.Damage.DamageDict["Airloss"] = regen;
            Dirty(uid, passive);
        }
    }
    private static bool BuyAction(BrainWormComponent wormcomp, float ammount)
    {
        if (wormcomp.Chemicals < ammount)
            return false;

        wormcomp.Chemicals -= ammount;
        return true;
    }
}
