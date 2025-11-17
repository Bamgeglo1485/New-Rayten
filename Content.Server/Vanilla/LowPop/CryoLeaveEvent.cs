using Robust.Shared.Player;

namespace Content.Server.Vanilla.LowPop;

[ByRefEvent]
public readonly struct CryoLeaveEvent
{
    public readonly string JobId;

    public CryoLeaveEvent(string jobId)
    {
        JobId = jobId;
    }
}
