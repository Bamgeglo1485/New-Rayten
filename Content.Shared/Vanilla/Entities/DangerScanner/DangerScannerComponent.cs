using Content.Shared.DoAfter;
using Content.Shared.Radio;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Vanilla.Entities.DangerScanner;

[RegisterComponent]
public sealed partial class DangerScannerComponent : Component
{
    [DataField]
    public float ScanDoAfterDuration = 5f;

    [DataField]
    public ProtoId<RadioChannelPrototype> SecurityChannel = "Security";

    [DataField]
    public SoundSpecifier? CompleteSound = new SoundPathSpecifier("/Audio/Items/beep.ogg");
}

[Serializable, NetSerializable]
public sealed partial class ScannerDoAfterEvent : SimpleDoAfterEvent
{
}
