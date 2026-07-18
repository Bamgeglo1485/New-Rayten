using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared.Evac.Components;

[RegisterComponent]
public sealed partial class EvacPodComponent : Component
{
    [DataField]
    public SoundSpecifier PreEvacSound = new SoundPathSpecifier("/Audio/Vanilla/Effects/evac.ogg");

    [DataField(customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string TeleportEffect = "EffectFlashBluespaceExplosion";
}
