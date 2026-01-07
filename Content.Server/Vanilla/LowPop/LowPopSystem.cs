using Content.Shared.Power.Components;
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
        SubscribeLocalEvent<CryoLeaveEvent>(OnPlayerLeave);
        //чек на количество сб
        SubscribeLocalEvent<ObjectiveSecCountRequirementComponent, RequirementCheckEvent>(OnCheck);
    }
    private void RebalanceLowPop()
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
                    recharger.AutoRechargeRate = battery.MaxCharge;
                }
            }
        }
        else
        {
            var query = EntityQueryEnumerator<SmesComponent>();
            while (query.MoveNext(out var uid, out _))
            {
                RemComp<BatterySelfRechargerComponent>(uid);
            }
        }
    }


    private void OnCheck(EntityUid uid, ObjectiveSecCountRequirementComponent comp, ref RequirementCheckEvent args)
    {
        if (args.Cancelled)
            return;

        if (GetSecurityCount() < comp.MinSec)
            args.Cancelled = true;
    }

    private void OnRoundStarting(RoundStartedEvent ev)
    {
        RebalanceLowPop();
    }


    private void OnPlayerLeave(ref CryoLeaveEvent ev)
    {
        Log.Warning($"ливнул {ev.JobId}");
        //инженер
        if (ev.JobId == "StationEngineer" || ev.JobId == "AtmosphericTechnician" || ev.JobId == "ChiefEngineer" || ev.JobId == "TechnicalAssistant")
            _engCount--;
        //учёный
        if (ev.JobId == "ResearchDirector" || ev.JobId == "Scientist" || ev.JobId == "ResearchAssistant")
            _sciCount--;
        //СБ
        if (ev.JobId == "HeadOfSecurity" || ev.JobId == "SecurityCadet" || ev.JobId == "Detective" || ev.JobId == "SecurityOfficer" || ev.JobId == "Warden")
            _secCount--;
        RebalanceLowPop();
    }

    private void OnPlayerSpawned(PlayerSpawnCompleteEvent ev)
    {
        //инженер
        if (ev.JobId == "StationEngineer" || ev.JobId == "AtmosphericTechnician" || ev.JobId == "ChiefEngineer" || ev.JobId == "TechnicalAssistant")
            _engCount++;
        //учёный
        if (ev.JobId == "ResearchDirector" || ev.JobId == "Scientist" || ev.JobId == "ResearchAssistant")
            _sciCount++;
        //СБ
        if (ev.JobId == "HeadOfSecurity" || ev.JobId == "SecurityCadet" || ev.JobId == "Detective" || ev.JobId == "SecurityOfficer" || ev.JobId == "Warden")
            _secCount++;
        RebalanceLowPop();
    }

    public int GetScientistCount()
    {
        return _sciCount;
    }

    public int GetSecurityCount()
    {
        return _secCount;
    }

    private void OnCleaning(RoundRestartCleanupEvent ev)
    {
        _engCount = 0;
        _sciCount = 0;
        _secCount = 0;
    }
}
