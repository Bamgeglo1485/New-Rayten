using Content.Server.Chat.Systems;
using Content.Shared.Vanilla.Entities.DangerScanner;
using Content.Shared.Interaction;
using Content.Shared.DoAfter;
using Content.Shared.Vanilla.Dominator;
using Content.Shared.Chat;
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

    private const float ScanCoolDown = 10f; //в минутах

    private Dictionary<EntityUid, TimeSpan> _scannedEntities = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<PortableDangerScannerComponent, AfterInteractEvent>(OnScannerAfterInteract);
        SubscribeLocalEvent<PortableDangerScannerComponent, ScannerDoAfterEvent>(OnDoAfter);
    }

    private void OnScannerAfterInteract(EntityUid uid, PortableDangerScannerComponent component, AfterInteractEvent args)
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
        _chat.TrySendInGameICMessage(uid, Loc.GetString("dominator-scanner-start"), InGameICChatType.Speak, true);

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, component.ScanDoAfterDuration, new ScannerDoAfterEvent(), uid, target: target, used: uid)
        {
            DistanceThreshold = 2f
        });
    }

    private void OnDoAfter(EntityUid uid, PortableDangerScannerComponent component, DoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Args.Target == null)
            return;

        _audio.PlayPvs(component.CompleteSound, uid);

        int targetdanger = _dangermob.GetEntityDanger(args.Args.Target.Value, deepseek: true);

        _chat.TrySendInGameICMessage(uid, Loc.GetString("dominator-scanner-end", ("danger", targetdanger)), InGameICChatType.Speak, true);

        // Обновляем время скана (или добавляем нового)
        _scannedEntities[args.Args.Target.Value] = _timing.CurTime;
        args.Handled = true;
    }

}
