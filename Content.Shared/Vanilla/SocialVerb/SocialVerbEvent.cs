using Content.Shared.Objectives;
using Robust.Shared.Serialization;

namespace Content.Shared.Vanilla.SocialVerb;

[Serializable, NetSerializable]
public sealed class SocialVerbEvent : EntityEventArgs
{
    public readonly string ID;
    public readonly NetEntity Target;
    public readonly NetEntity? User;
    public readonly NetEntity? Item;
    public readonly bool IsResponse;

    public SocialVerbEvent(string id, NetEntity target, NetEntity? item, NetEntity? user = null, bool isResponse = false)
    {
        Target = target;
        Item = item;
        ID = id;
        User = user;
        IsResponse = isResponse;
    }
}