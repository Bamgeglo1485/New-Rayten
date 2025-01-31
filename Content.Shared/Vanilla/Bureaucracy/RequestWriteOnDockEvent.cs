using Content.Shared.Objectives;
using Robust.Shared.Serialization;

namespace Content.Shared.Vanilla.Bureaucracy;

[Serializable, NetSerializable]
public sealed class RequestWriteOnDockEvent : EntityEventArgs
{
    public readonly NetEntity paper;
    public readonly string id;
    public RequestWriteOnDockEvent(NetEntity Paper, string ID)
    {
        paper = Paper;
        id = ID;
    }

}
