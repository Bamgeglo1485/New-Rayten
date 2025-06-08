using Content.Server.Communications;
using Content.Shared.GameTicking;
using Content.Shared.Vanilla.Background;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Content.Shared.Vanilla.Jammer;

namespace Content.Server.Vanilla.Jammer;

public sealed class JammerSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<OverrideJammerTimeEvent>(Onoverride);
        SubscribeLocalEvent<SetJammerOnSpawnComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CommunicationConsoleCallShuttleAttemptEvent>(OnShuttleCallAttempt);
    }

    private void OnMapInit(EntityUid uid, SetJammerOnSpawnComponent component, MapInitEvent args)
    {
        SetJammer(component.Duration);
    }

    public void SetJammer(TimeSpan duration, EntityUid? station = null)
    {
        var newendtime = _timing.CurTime + duration;
        var query = EntityQueryEnumerator<StationJammerComponent>();
        while (query.MoveNext(out var stationUid, out var jammer))
        {
            if (station != null && station != stationUid)
                continue;

            if (jammer.JammerEndTime == null || newendtime > jammer.JammerEndTime)
                jammer.JammerEndTime = newendtime;
        }
    }

    public void RemoveJammer(EntityUid? station = null)
    {
        var query = EntityQueryEnumerator<StationJammerComponent>();
        while (query.MoveNext(out var stationUid, out var jammer))
        {
            if (station != null && station != stationUid)
                continue;

            jammer.JammerEndTime = null;
        }
    }

    public (bool isActive, TimeSpan timetobreak) CheckJammer(EntityUid? station = null)
    {
        var curtime = _timing.CurTime;
        var query = EntityQueryEnumerator<StationJammerComponent>();
        while (query.MoveNext(out var stationUid, out var jammer))
        {
            if (station != null && station != stationUid)
                continue;

            if (jammer.JammerEndTime != null)
            {
                if (curtime > jammer.JammerEndTime)
                    RemoveJammer(stationUid);
                else
                    return (true, jammer.JammerEndTime.Value - curtime);
            }

        }
        return (false, TimeSpan.Zero);
    }

    private void Onoverride(OverrideJammerTimeEvent ev)
    {
        SetJammer(TimeSpan.FromMinutes(ev.Minutes));
    }

    private void OnShuttleCallAttempt(ref CommunicationConsoleCallShuttleAttemptEvent ev)
    {
        var (isjammeractive, timetobreak) = CheckJammer();

        if (isjammeractive)
            return;

        ev.Cancelled = true;
        ev.Reason = Loc.GetString("jammer-shuttle-call-unavailable");
    }
}
