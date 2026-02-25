using Content.Server.Communications;
using Content.Shared.Vanilla.Jammer;

namespace Content.Server.Vanilla.Jammer;

public sealed class JammerSystem : SharedJammerSystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CommunicationConsoleCallShuttleAttemptEvent>(OnShuttleCallAttempt);
        SubscribeLocalEvent<OverrideJammerTimeEvent>(Onoverride);
        SubscribeLocalEvent<SetJammerOnSpawnComponent, MapInitEvent>(SetJammerOnSpawn);
    }
    private void OnShuttleCallAttempt(ref CommunicationConsoleCallShuttleAttemptEvent ev)
    {
        var (isjammeractive, _) = CheckJammer();

        if (!isjammeractive)
            return;

        ev.Cancelled = true;
        ev.Reason = Loc.GetString("jammer-shuttle-call-unavailable");
    }
    private void Onoverride(OverrideJammerTimeEvent ev)
    {
        SetJammer(TimeSpan.FromMinutes(ev.Minutes));
    }
    private void SetJammerOnSpawn(EntityUid uid, SetJammerOnSpawnComponent component, MapInitEvent args)
    {
        SetJammer(component.Duration);
    }
    public void SetJammer(TimeSpan duration, EntityUid? station = null)
    {
        var newendtime = Timing.CurTime + duration;
        var query = EntityQueryEnumerator<StationJammerComponent>();
        while (query.MoveNext(out var stationUid, out var jammer))
        {
            if (station != null && station != stationUid)
                continue;

            if (jammer.JammerEndTime == null || newendtime > jammer.JammerEndTime)
                jammer.JammerEndTime = newendtime;

            Dirty(stationUid, jammer);
        }
    }
}
