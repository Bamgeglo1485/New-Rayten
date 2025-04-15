using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Client.Vanilla.UndertaleSpeech;


[RegisterComponent]
public sealed partial class UndertaleSpeechComponent : Component
{
    public string? RemainingText;
    public TimeSpan NextBeepTime;
    public SoundSpecifier? Sound;
    public bool iswhisper = false;
}