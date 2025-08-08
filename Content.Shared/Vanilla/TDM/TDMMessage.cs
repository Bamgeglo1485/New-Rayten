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


/// TTT
[Serializable, NetSerializable]
public sealed class TPMeToTTTEvent : EntityEventArgs
{
    public TPMeToTTTEvent()
    {
    }
}

[Serializable, NetSerializable]
public sealed class TTTInfoRequest : EntityEventArgs
{
    public TTTInfoRequest()
    {
    }
}

[Serializable, NetSerializable]
public sealed class TTTInformation : EntityEventArgs
{
    public int PlayerCount { get; }
    public TimeSpan TimeToStart { get; }
    public bool CanJoin  { get; }
    public TTTInformation(int playercount, TimeSpan timeToStart, bool canJoin)
    {
        PlayerCount = playercount;
        TimeToStart = timeToStart;
        CanJoin = canJoin;
    }
}
