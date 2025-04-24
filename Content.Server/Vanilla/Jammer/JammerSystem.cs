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
    private bool _isJammerActive = false;
    private TimeSpan? _jammerEndTime = null;
    private TimeSpan defaultjammertime = TimeSpan.FromMinutes(35);
    private TimeSpan AddictiveTime = TimeSpan.FromMinutes(0);
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundEndMessageEvent>(OnRoundEnd);
        SubscribeLocalEvent<OverrideJammerTimeEvent>(Onoverride);
        SubscribeLocalEvent<SetJammerOnSpawnComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CommunicationConsoleCallShuttleAttemptEvent>(OnShuttleCallAttempt);
    }
    private void OnMapInit(EntityUid uid, SetJammerOnSpawnComponent component, MapInitEvent args)
    {
        SetJammer(component.Duration);
        //RemComp<SetJammerOnSpawnComponent>(uid);
    }

    public void TrySetJammer()
    {
        if(_isJammerActive)
            return;
        SetJammer(defaultjammertime);
    }
    
    public void SetJammer(TimeSpan duration)
    {
        _isJammerActive = true;
        _jammerEndTime = _timing.CurTime + duration + AddictiveTime;
    }

    public void RemoveJammer()
    {
        _isJammerActive = false;
        _jammerEndTime = null;
    }

    public TimeSpan CheckJammer()
    {
        if (!_isJammerActive || _jammerEndTime == null)
            return TimeSpan.Zero;

        var remainingTime = _jammerEndTime - _timing.CurTime;
        if (remainingTime <= TimeSpan.Zero)
        {
            RemoveJammer();
            return TimeSpan.Zero;
        }

        return remainingTime.Value;
    }

    private void Onoverride(OverrideJammerTimeEvent ev)
    {
        AddictiveTime = TimeSpan.FromMinutes(ev.Minutes);
    }

    private void OnRoundEnd(RoundEndMessageEvent ev)
    {
        RemoveJammer();
        AddictiveTime = TimeSpan.FromMinutes(0);
    }
    private void OnShuttleCallAttempt(ref CommunicationConsoleCallShuttleAttemptEvent ev)
    {
        if (CheckJammer() == TimeSpan.Zero)
            return;
        ev.Cancelled = true;
        ev.Reason = Loc.GetString("jammer-shuttle-call-unavailable");
    }
}