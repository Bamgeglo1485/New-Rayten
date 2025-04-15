using Content.Shared.Humanoid;
using Robust.Shared.Prototypes;
using Robust.Shared.Audio;

namespace Content.Shared.Vanilla.UndertaleSpeech;

[Prototype("UndertaleSpeech")]
public sealed class UndertaleSpeechrototype : IPrototype
{
    [IdDataField]
    public string ID { get; } = default!;

    [DataField("name")]
    public string Name { get; } = string.Empty;

    [DataField("sex", required: true)]
    public Sex Sex { get; } = default!;

    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("voice", required: true)]
    public SoundSpecifier? Voice = new SoundPathSpecifier("/Audio/Vanilla/Effects/R1/undertale_beep.ogg");

    [DataField("roundStart")]
    public bool RoundStart { get; } = true;

    [DataField("sponsorOnly")]
    public bool SponsorOnly { get; } = false;
}