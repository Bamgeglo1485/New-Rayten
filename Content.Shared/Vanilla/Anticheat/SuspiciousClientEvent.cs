using Content.Shared.Objectives;
using Robust.Shared.Serialization;

namespace Content.Shared.Vanilla.Anticheat;

[Serializable, NetSerializable]
public sealed class SuspiciousClientEvent : EntityEventArgs
{
    public readonly string Reason;

    public SuspiciousClientEvent(string reason)
    {
        Reason = reason;
    }
}
