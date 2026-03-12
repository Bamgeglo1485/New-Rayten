using Content.Shared.Vanilla.Games.TTT.Items.DNAScanner;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Trigger.Components;
using Content.Shared.Trigger.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.DoAfter;
using Content.Shared.Popups;
using Content.Shared.Interaction;
using Robust.Shared.Audio;
using Robust.Shared.Timing;
using Robust.Shared.Random;
using Content.Shared.Random.Helpers;

namespace Content.Shared.Vanilla.Games.Items.TTT;

public sealed class TTTBombSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly TriggerSystem _trigger = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly TTTDnaScannerSystem _dnaScanner = default!;


    private float _timeAcummulator = 0;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TTTBombComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<TTTBombComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<TTTBombComponent, TTTDefuseIvent>(OnDefuse);
        SubscribeLocalEvent<TTTBombComponent, InteractUsingEvent>(OnInteract);
    }
    public override void Update(float frameTime)
    {
        _timeAcummulator += frameTime;
        if (_timeAcummulator < 0.2f)
            return;
        _timeAcummulator = 0;
        base.Update(frameTime);
        var query = EntityQueryEnumerator<TTTBombComponent, TimerTriggerComponent, ActiveTimerTriggerComponent>();
        while (query.MoveNext(out var uid, out var bomb, out var timerTrigger, out _))
        {
            var timetoTrigger = timerTrigger.NextTrigger - _timing.CurTime;
            timerTrigger.BeepInterval = timetoTrigger / 30;
            Dirty(uid, timerTrigger);
        }
    }
    private void OnInteract(EntityUid uid, TTTBombComponent component, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;
        if (!HasComp<ActiveTimerTriggerComponent>(uid))
        {
            if (HasComp<TTTDnaScannerComponent>(args.Used) && component.User != null)
                _dnaScanner.TrySetTarget(args.Used, component.User.Value);
            return;
        }
        if (!HasComp<DifusalKitComponent>(args.Used))
            return;
        _popup.PopupEntity("УСПЕХ!", uid, PopupType.LargeCaution);
        _trigger.StopTimerTrigger(uid);
        args.Handled = true;
    }

    private void OnActivate(EntityUid uid, TTTBombComponent component, ref ActivateInWorldEvent args)
    {
        if (!HasComp<ActiveTimerTriggerComponent>(uid))
            return;

        var doAfterArgs = new DoAfterArgs(EntityManager, args.User, TimeSpan.FromSeconds(5), new TTTDefuseIvent(), uid)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            NeedHand = true,
        };
        _doAfter.TryStartDoAfter(doAfterArgs);
    }

    private void OnDefuse(EntityUid uid, TTTBombComponent component, TTTDefuseIvent args)
    {
        if (args.Cancelled || args.Handled)
            return;
        if (!HasComp<ActiveTimerTriggerComponent>(uid))
            return;
        if (!TryComp<TimerTriggerComponent>(uid, out var timerTrigger))
            return;
        var rand = SharedRandomExtensions.PredictedRandom(_timing, GetNetEntity(uid));
        if (rand.Prob(component.DifuseChance))
        {
            _popup.PopupPredicted("УСПЕХ!", args.User, args.User, PopupType.LargeCaution);
            _trigger.StopTimerTrigger((uid, timerTrigger));
            return;
        }
        _popup.PopupPredicted("БЛЯТЬ", args.User, args.User, PopupType.LargeCaution);
        timerTrigger.NextTrigger = _timing.CurTime + TimeSpan.FromSeconds(3);
        Dirty(uid, timerTrigger);
    }
    private void OnUseInHand(EntityUid uid, TTTBombComponent component, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;
        if (!TryComp<TimerTriggerComponent>(uid, out var timerTrigger))
            return;
        component.User = args.User;
        (component.DifuseChance, var volume, var range) = timerTrigger.Delay.TotalSeconds switch
        {
            >= 45 and <= 60 => (0.83f, -16f, 15f),
            > 60 and <= 120 => (0.67f, -18f, 10f),
            > 120 and <= 180 => (0.50f, -21f, 5f),
            > 180 and <= 240 => (0.34f, -23f, 5f),
            _ => (0.17f, -25f, 5f)
        };
        timerTrigger.BeepSound = new SoundPathSpecifier("/Audio/Machines/Nuke/general_beep.ogg",
            AudioParams.Default.WithVolume(volume).WithMaxDistance(range));
        _hands.TryDrop(args.User, uid);
        RemComp<ItemComponent>(uid);
        RemComp<PullableComponent>(uid);
        _trigger.Trigger(uid, args.User, TriggerSystem.DefaultTriggerKey);
        var timetoTrigger = timerTrigger.NextTrigger - _timing.CurTime;
        timerTrigger.BeepInterval = timetoTrigger / 30;
        Dirty(uid, timerTrigger);
        args.Handled = true;
    }
}
