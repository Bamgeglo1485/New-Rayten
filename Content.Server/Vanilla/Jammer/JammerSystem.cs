using Content.Server.Communications;
using Content.Server.Vanilla.EventTeam;
using Content.Server.NukeOps;
using Content.Shared.GameTicking;
using Content.Shared.NukeOps;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Vanilla.Jammer;

public sealed class JammerSystem : EntitySystem
{
    private bool _isJammerActive = false;
    private TimeSpan? _jammerEndTime = null;
    private const string _ertproto = "ERT";
    private TimeSpan defaultjammertime = TimeSpan.FromMinutes(35);

    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly EventTeamSystem _eventteam = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundEndMessageEvent>(OnRoundEnd);
        SubscribeLocalEvent<CommunicationConsoleCallShuttleAttemptEvent>(OnShuttleCallAttempt);
    }

    private void OnRoundEnd(RoundEndMessageEvent ev)
    {
        RemoveJammer(true);
    }

    private void NukeStartjammer(Entity<WarDeclaratorComponent> ent, ref MapInitEvent args)
    {
        float timer = ent.Comp.WarDeclarationDelay;

    }
    public void TrySetJammer()
    {
        if(_isJammerActive)
            return;
        _isJammerActive = true;
        _jammerEndTime = _timing.CurTime + defaultjammertime;
    }
    public void SetJammer()
    {
        _isJammerActive = true;
        _jammerEndTime = _timing.CurTime + defaultjammertime;
    }

    public void SetJammer(TimeSpan jammerDuration)
    {
        _isJammerActive = true;
        _jammerEndTime = _timing.CurTime + jammerDuration;
    }

    public void RemoveJammer(bool noert = false)
    {
        _isJammerActive = false;
        _jammerEndTime = null;
        if(noert)
            return;
        if(!_prototypes.TryIndex<EventTeamPrototype>(_ertproto, out _))
        {
            Logger.Error("ERT prototype is incorrect.");
            return;
        }
        _eventteam.call(_ertproto);
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

    private void OnShuttleCallAttempt(ref CommunicationConsoleCallShuttleAttemptEvent ev)
    {
        if (CheckJammer() == TimeSpan.Zero)
            return;

        ev.Cancelled = true;
        ev.Reason = Loc.GetString("jammer-shuttle-call-unavailable");
    }
}
