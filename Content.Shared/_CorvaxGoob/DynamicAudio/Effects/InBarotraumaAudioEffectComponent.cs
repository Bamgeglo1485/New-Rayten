using Robust.Shared.GameStates;

namespace Content.Shared._CorvaxGoob.DynamicAudio.Effects;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class InBarotraumaAudioEffectComponent : Component
{
    // Штука для синхронизации с клиентом
    [DataField, AutoNetworkedField]
    public bool GegloAhuenen = true;
}
