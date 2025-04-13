using Robust.Shared.Serialization;
using Robust.Shared.Prototypes;

namespace Content.Shared.Vanilla.Background;

[Serializable, NetSerializable]
public sealed class TakeGhostBackgroundEvent : EntityEventArgs
{
    public readonly ProtoId<BackgroundPrototype> Background; 
    public TakeGhostBackgroundEvent(ProtoId<BackgroundPrototype> background)
    {
        Background = background;
    }
}