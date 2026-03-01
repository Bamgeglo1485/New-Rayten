using Robust.Shared.Serialization;

namespace Content.Shared.Vanilla.Anticheat;

[Serializable, NetSerializable]
public sealed class SuspiciousClientEvent(string reason, bool withBan) : EntityEventArgs
{
    public readonly string Reason = reason;
    public readonly bool WithBan = withBan;
}
