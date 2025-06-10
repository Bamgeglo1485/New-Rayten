using Content.Server.Communications;
using Content.Shared.Vanilla.Background;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;
using Content.Shared.Vanilla.Jammer;

namespace Content.Server.Vanilla.Jammer;

public sealed class JammerSystem : SharedJammerSystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<CommunicationConsoleCallShuttleAttemptEvent>(OnShuttleCallAttempt);
    }

    private void OnShuttleCallAttempt(ref CommunicationConsoleCallShuttleAttemptEvent ev)
    {
        var (isjammeractive, timetobreak) = CheckJammer();

        if (!isjammeractive)
            return;

        ev.Cancelled = true;
        ev.Reason = Loc.GetString("jammer-shuttle-call-unavailable");
    }
}
