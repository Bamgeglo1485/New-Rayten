using Content.Server.Power.Components;
using Content.Shared.GameTicking;
using Content.Server.Power.SMES;
using Content.Server.Vanilla.Objectives.Components;
using Content.Shared.Objectives.Components;

namespace Content.Server.Vanilla.LowPop;

public sealed class LowPopSystem : EntitySystem
{
    private int _engCount = 0;
    private int _sciCount = 0;
    private int _secCount = 0;

    public override void Initialize()
    {
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawned);
        SubscribeLocalEvent<RoundStartedEvent>(OnRoundStarting);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnCleaning);
        //чек на количество сб
        SubscribeLocalEvent<ObjectiveSecCountRequirementComponent, RequirementCheckEvent>(OnCheck);
    }

    private void OnCheck(EntityUid uid, ObjectiveSecCountRequirementComponent comp, ref RequirementCheckEvent args)
    {
        if (args.Cancelled)
            return;

        if (GetSecurityCount() < comp.MinSec)
            args.Cancelled = true;
    }

    private void OnCleaning(RoundRestartCleanupEvent ev)
    {
        _engCount = 0;
        _sciCount = 0;
        _secCount = 0;
    }

    private void OnRoundStarting(RoundStartedEvent ev)
    {
        //инженеры
        if (_engCount == 0)
        {
            var query = EntityQueryEnumerator<SmesComponent>();
            while (query.MoveNext(out var uid, out _))
            {
                if (TryComp<BatteryComponent>(uid, out var battery))
                {
                    var recharger = EnsureComp<BatterySelfRechargerComponent>(uid);
                    recharger.AutoRecharge = true;
                    recharger.AutoRechargeRate = battery.MaxCharge;
                    recharger.AutoRechargePause = false;
                }
            }
        }
    }

    private void OnPlayerSpawned(PlayerSpawnCompleteEvent ev)
    {
        //инженер
        if (ev.JobId == "StationEngineer" || ev.JobId == "AtmosphericTechnician" || ev.JobId == "ChiefEngineer" || ev.JobId == "TechnicalAssistant")
        {
            _engCount++;
            if (_engCount == 1)
            {
                var query = EntityQueryEnumerator<SmesComponent>();
                while (query.MoveNext(out var uid, out _))
                {
                    RemComp<BatterySelfRechargerComponent>(uid);
                }
            }
        }
        //учёный
        if (ev.JobId == "ResearchDirector" || ev.JobId == "Scientist" || ev.JobId == "ResearchAssistant")
            _sciCount++;
        //СБ
        if (ev.JobId == "HeadOfSecurity" || ev.JobId == "SecurityCadet" || ev.JobId == "SecurityOfficer" || ev.JobId == "Warden")
            _secCount++;
    }

    public int GetScientistCount()
    {
        return _sciCount;
    }

    public int GetSecurityCount()
    {
        return _secCount;
    }
}
