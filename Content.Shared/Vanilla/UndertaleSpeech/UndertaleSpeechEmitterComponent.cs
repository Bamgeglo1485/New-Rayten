using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Content.Shared.Actions;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Audio;

namespace Content.Shared.Vanilla.UndertaleSpeech;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class UndertaleSpeechEmitterComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite),AutoNetworkedField]
    [DataField("voice", customTypeSerializer: typeof(PrototypeIdSerializer<UndertaleSpeechPrototype>))]
    public string? VoicePrototypeId { get; set; }

    [DataField("pitch"), AutoNetworkedField]
    public float Pitch = 1.0f;

    public SoundSpecifier Voice = new SoundPathSpecifier("/Audio/Vanilla/Effects/undertale/SANS.ogg");

    public bool iswhisper = false;
}