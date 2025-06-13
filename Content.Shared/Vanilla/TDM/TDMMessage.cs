using Robust.Shared.Serialization;

namespace Content.Shared.Vanilla.TDM;

[Serializable, NetSerializable]
public sealed class TPMeToTDMEvent : EntityEventArgs
{
    public TPMeToTDMEvent()
    {
    }
}

[Serializable, NetSerializable]
public sealed class TDMInfoRequest : EntityEventArgs
{
    public TDMInfoRequest()
    {
    }
}

[Serializable, NetSerializable]
public sealed class TDMInformation : EntityEventArgs
{
    public int PlayerCount { get; }
    public TimeSpan TimeToStart { get; }
    public bool CanJoin  { get; }
    public TDMInformation(int playercount, TimeSpan timeToStart, bool canJoin)
    {
        PlayerCount = playercount;
        TimeToStart = timeToStart;
        CanJoin = canJoin;
    }
}
