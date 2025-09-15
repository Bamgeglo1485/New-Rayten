using Content.Shared.DoAfter;
using Content.Shared.Humanoid;
using Content.Shared.Mindshield.Components;
using Content.Shared.IdentityManagement;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Shared.Player;
namespace Content.Shared.Vanilla.Entities.BrainWorm;

public partial class BrainWormSystem : EntitySystem
{
    [Dependency] private readonly MobStateSystem _mob = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    private void OnReturnControl(EntityUid uid, BrainWormHostComponent component, BrainWormReturnControlActionEvent args)
    {
        var ev = new ReControlEvent();
        RaiseLocalEvent(uid, ref ev);
        args.Handled = true;
    }

    private void OnChemicals(EntityUid uid, BrainWormComponent component, BrainWormChemicalsActionEvent args)
    {
        if (!TryComp<ActorComponent>(uid, out var actor))
            return;

        if (!_ui.TryToggleUi(uid, BrainWormComponent.ChemicalsUiKey.Key, actor.PlayerSession))
            return;
    }


    private void OnForceSay(EntityUid uid, BrainWormComponent component, BrainWormForceSayActionEvent args)
    {
        if (!TryComp<ActorComponent>(uid, out var actor))
            return;

        if (!_ui.TryToggleUi(uid, BrainWormComponent.ForceSayUiKey.Key, actor.PlayerSession))
            return;
    }

    private void EjectBrain(EntityUid uid, BrainWormComponent component, EjectBrainEvent args)
    {
        if (!component.Host.HasValue)
            return;

        if (component.IsSleep)
            return;

        var doAfterEventArgs = new DoAfterArgs(EntityManager, uid, BrainWormComponent.EjectBrainTime, new EjectBrainDoAfterEvent(), eventTarget: uid, target: component.Host.Value)
        {
            DistanceThreshold = 2f,
            BreakOnMove = true,
            Hidden = true
        };

        if (!_doAfter.TryStartDoAfter(doAfterEventArgs, out component.EjectDoAfter))
            return;

        args.Handled = true;
    }

    public void InsertInBrain(InsertInBrainEvent ev)
    {
        if (ev.Handled)
            return;

        var worm = ev.Performer;
        var target = ev.Target;

        if (!TryComp<BrainWormComponent>(worm, out var wormcomp))
            return;

        //Если в мозге уже есть другой червь
        if (HasComp<BrainWormHostComponent>(target))
        {
            _popup.PopupClient(Loc.GetString("brainworm-popup-host-already-wormed"), worm, worm, PopupType.Medium);
            return;
        }

        if (!HasComp<HumanoidAppearanceComponent>(target))
        {
            _popup.PopupClient(Loc.GetString("brainworm-popup-host-not-humanoid"), worm, worm, PopupType.Medium);
            return;
        }

        var doAfterEventArgs = new DoAfterArgs(EntityManager, worm, wormcomp.InsertDoAfterTime, new InsertBrainDoAfterEvent(), eventTarget: worm, target: target)
        {
            DistanceThreshold = 2f,
            BreakOnMove = true,
            Hidden = true
        };

        if (!_doAfter.TryStartDoAfter(doAfterEventArgs))
            return;

        ev.Handled = true;
    }

    private void OnMindControl(EntityUid uid, BrainWormComponent component, MindControlEvent args)
    {
        if (!component.Host.HasValue)
            return;

        if (component.IsSleep)
            return;
        var host = component.Host.Value;
        if (HasComp<MindShieldComponent>(host))
        {
            _popup.PopupClient(Loc.GetString("brainworm-popup-host-mindshield"), uid, uid, PopupType.Medium);
            return;
        }

        if (!_mob.IsAlive(host))
        {
            _popup.PopupClient(Loc.GetString("brainworm-popup-host-not-alive"), uid, uid, PopupType.Medium);
            return;
        }

        _popup.PopupClient(Loc.GetString("brainworm-host-mind-control", ("user", Identity.Entity(uid, EntityManager))), host, host, PopupType.Medium);

        var doAfterEventArgs = new DoAfterArgs(EntityManager, uid, component.MindControlDoAfterTime, new MindControlDoAfterEvent(), eventTarget: uid, target: component.Host.Value)
        {
            DistanceThreshold = 2f,
            BreakOnMove = true,
            Hidden = true
        };

        if (!_doAfter.TryStartDoAfter(doAfterEventArgs, out component.MindControlDoAfter))
            return;

        args.Handled = true;
    }

}
