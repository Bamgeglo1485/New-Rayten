using Content.Server.Chat.Systems;
using Content.Server.Radio.EntitySystems;
using Content.Server.CriminalRecords.Systems;
using Content.Server.Station.Systems;

using Content.Shared.Vanilla.Entities.DangerScanner;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.DoAfter;
using Content.Shared.Vanilla.Dominator;
using Content.Shared.Chat;
using Content.Shared.Contraband;
using Content.Shared.StationRecords;
using Content.Shared.Security;
using Content.Shared.StepTrigger.Systems;

using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server.Vanilla.Entities.DangerScanner;

public sealed class DangerScannerSystem : EntitySystem
{
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedDangerMobSystem _dangermob = default!;
    [Dependency] private readonly SharedStationRecordsSystem _records = default!;
    [Dependency] private readonly CriminalRecordsSystem _criminalRecords = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly RadioSystem _radio = default!;

    private const float ScanCoolDown = 10f; //в минутах
    private Dictionary<EntityUid, TimeSpan> _scannedEntities = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<DangerScannerComponent, AfterInteractEvent>(OnScannerAfterInteract);
        SubscribeLocalEvent<DangerScannerComponent, ScannerDoAfterEvent>(OnDoAfter);
        SubscribeLocalEvent<DangerScannerComponent, StepTriggeredOnEvent>(OnStepTrigger);
    }
    private void OnStepTrigger(EntityUid uid, DangerScannerComponent component, ref StepTriggeredOnEvent args)
    {
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

        // Проверяем был ли уже скан
        if (_scannedEntities.TryGetValue(target, out var lastScan))
        {
            // Кулдаун 5 минут
            var cooldown = TimeSpan.FromMinutes(ScanCoolDown);
            var remaining = (lastScan + cooldown) - curTime;

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
                return;
            }
        }

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, component.ScanDoAfterDuration, new ScannerDoAfterEvent(), uid, target: target, used: uid)
        {
            DistanceThreshold = 2f
        });
    }

    private void OnDoAfter(EntityUid uid, DangerScannerComponent component, DoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Args.Target == null)
            return;

        var target = args.Args.Target.Value;
        _audio.PlayPvs(component.CompleteSound, uid);

        if (_dangermob.TryGetDangeriousItem(target, out var item) && item.HasValue && TryComp<ContrabandComponent>(item, out var contraband))
        {
            _chat.TrySendInGameICMessage(uid, Loc.GetString("dominator-scanner-end-danger", ("item", Name(item.Value))), InGameICChatType.Speak, true);
            SetWanted(uid, component, Name(target), item.Value, contraband);
        }
        else
        {
            _chat.TrySendInGameICMessage(uid, Loc.GetString("dominator-scanner-end-no-danger"), InGameICChatType.Speak, true);
        }
        // Обновляем время скана (или добавляем новую запись)
        _scannedEntities[target] = _timing.CurTime;
        args.Handled = true;
    }

    private void SetWanted(EntityUid scanner, DangerScannerComponent component, string target, EntityUid item, ContrabandComponent contraband)
    {
        if (_station.GetOwningStation(scanner) is { } station)
        {
            var id = _records.GetRecordByName(station, target);
            if (id != null)
            {
                var key = new StationRecordKey(id.Value, station);
                var reason = Loc.GetString("scanner-set-wanted", ("Severity", contraband.Severity), ("item", Name(item)));
                if (_criminalRecords.TryChangeStatus(key, SecurityStatus.Wanted, reason, Name(scanner)))
                {
                    _radio.SendRadioMessage(scanner,
                        Loc.GetString("scanner-radio-message", ("name", target), ("reason", reason)),
                        component.SecurityChannel, scanner);
                }
            }
        }
    }
}
