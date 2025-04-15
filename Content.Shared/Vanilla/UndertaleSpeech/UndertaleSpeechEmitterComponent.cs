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
    [DataField("voice", customTypeSerializer: typeof(PrototypeIdSerializer<UndertaleSpeechrototype>))]
    public string? VoicePrototypeId { get; set; }
}