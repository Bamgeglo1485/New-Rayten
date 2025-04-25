using Lidgren.Network;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared.Vanilla.Sponsor;

public sealed class SetSponsorRank : NetMessage
{
    public sponsorRank rank = sponsorRank.None;

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        rank = (sponsorRank)buffer.ReadInt32();
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        buffer.Write((int)rank);
    }
    public override MsgGroups MsgGroup => MsgGroups.Command;
}

[Serializable, NetSerializable]
public enum sponsorRank
{
    None = 0,
    GrayTide = 1,
    Revolutionary = 2,
    Syndicate = 3, 
    SpaceNinja = 4
}