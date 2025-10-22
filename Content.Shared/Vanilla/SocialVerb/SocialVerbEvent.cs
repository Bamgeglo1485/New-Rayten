using Content.Shared.Objectives;
using Robust.Shared.Serialization;

namespace Content.Shared.Vanilla.SocialVerb;

[Serializable, NetSerializable]
public sealed class SocialVerbEvent : EntityEventArgs
{
    public readonly NetEntity Target;
    public readonly NetEntity? Item;
    public readonly string ID;

    public SocialVerbEvent(string id, NetEntity target, NetEntity? item)
    {
        Target = target;
        Item = item;
        ID = id;
    }

}