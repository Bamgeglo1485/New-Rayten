using Content.Shared.Power.EntitySystems;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.DoAfter;
using Content.Shared.Vanilla.Dominator;
using Content.Shared.Chat;
using Content.Shared.Contraband;
using Content.Shared.StepTrigger.Systems;
using Content.Shared.Chat;

using Robust.Shared.Prototypes;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Shared.Vanilla.Entities.DangerScanner;

public abstract class SharedDangerScannerSystem : EntitySystem
{
    [Dependency] private readonly SharedChatSystem _chat = default!;
    [Dependency] protected readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedDangerMobSystem _dangermob = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedPowerReceiverSystem _power = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<DangerScannerComponent, AfterInteractEvent>(OnScannerAfterInteract);
        SubscribeLocalEvent<DangerScannerComponent, ScannerDoAfterEvent>(OnDoAfter);
        SubscribeLocalEvent<DangerScannerComponent, StepTriggeredOnEvent>(OnStepTrigger);
        SubscribeLocalEvent<DangerScannerComponent, StepTriggerAttemptEvent>(HandleStepTriggerAttempt);
    }

    private void HandleStepTriggerAttempt(EntityUid uid, DangerScannerComponent component, ref StepTriggerAttemptEvent args)
    {
        args.Continue = _power.IsPowered(uid);
    }

    private void OnStepTrigger(EntityUid uid, DangerScannerComponent component, ref StepTriggeredOnEvent args)
    {
        var target = args.Tripper;
        string scanLayer;

        if (_dangermob.TryGetDangeriousItem(target, out var item)
            && item.HasValue
            && TryComp<ContrabandComponent>(item, out var contraband)
            && _proto.TryIndex<ContrabandSeverityPrototype>(contraband.Severity, out var severityProto))
        {
            SetWanted(uid, component, Name(target), item.Value, contraband);
            _audio.PlayPredicted(component.AlarmSound, uid, target);
            scanLayer = severityProto.ID switch
            {
                "ThirdLevel" => "Lethal",
                "ThirdLevelRestricted" => "Lethal",
                "GrandTheft" => "Lethal",
                _ => "Stun"
            };
        }
        else
        {
            _audio.PlayPredicted(component.CompleteSound, uid, target);
            scanLayer = "Safe";
        }

        PlayScanAnimation(uid, scanLayer);
    }

    private void OnScannerAfterInteract(EntityUid uid, DangerScannerComponent component, AfterInteractEvent args)
    {
        if (args.Target is not { } target)
            return;

        if (!HasComp<DangerMobComponent>(target))
            return;

        if (!args.CanReach)
            return;

        var curTime = _timing.CurTime;
        //Если еще не сканировали, или если прошел кулдаун - сканируем
        if (!TryComp<DangerScannedComponent>(target, out var scannedComp) || curTime > scannedComp.NextScanIn)
        {
            _audio.PlayPredicted(component.ScanSound, uid, args.User);
            _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, component.ScanDoAfterDuration, new ScannerDoAfterEvent(), uid, target: target, used: uid)
            {
                DistanceThreshold = 2f
            });
            return;
        }

        var remaining = scannedComp.NextScanIn - curTime;
        if (remaining > TimeSpan.Zero)
        {
            string timeText;
            if (remaining.TotalMinutes >= 1)
            {
                if (remaining.Seconds > 0)
                    timeText = $"{remaining.Minutes} мин. {remaining.Seconds} сек.";
                else
                    timeText = $"{remaining.Minutes} мин.";
            }
            else
            {
                timeText = $"{remaining.Seconds} сек.";
            }

            _chat.TrySendInGameICMessage(uid,
                Loc.GetString("dominator-scanner-cooldown", ("time", timeText)),
                InGameICChatType.Speak, true);
        }
    }

    private void OnDoAfter(EntityUid uid, DangerScannerComponent component, DoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Args.Target == null)
            return;

        var target = args.Args.Target.Value;
        var user = args.Args.User;
        if (_dangermob.TryGetDangeriousItem(target, out var item) && item.HasValue && TryComp<ContrabandComponent>(item, out var contraband))
        {
            _audio.PlayPredicted(component.AlarmSound, uid, user);
            _chat.TrySendInGameICMessage(uid, Loc.GetString("dominator-scanner-end-danger", ("item", Name(item.Value))), InGameICChatType.Speak, true);
            SetWanted(uid, component, Name(target), item.Value, contraband);
        }
        else
        {
            _audio.PlayPredicted(component.CompleteSound, uid, user);
            _chat.TrySendInGameICMessage(uid, Loc.GetString("dominator-scanner-end-no-danger"), InGameICChatType.Speak, true);
        }
        var scannedComp = EnsureComp<DangerScannedComponent>(target);
        scannedComp.NextScanIn = _timing.CurTime + TimeSpan.FromMinutes(DangerScannedComponent.ScanCoolDown);
        // Dirty(target, scannedComp);
        args.Handled = true;
    }

    //server-only
    protected abstract void SetWanted(EntityUid scanner, DangerScannerComponent component, string target, EntityUid item, ContrabandComponent contraband);
    //client-only
    protected abstract void PlayScanAnimation(EntityUid uid, string scanLayer);
}
