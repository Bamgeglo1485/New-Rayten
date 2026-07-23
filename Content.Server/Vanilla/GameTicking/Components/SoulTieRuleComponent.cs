using Robust.Shared.GameObjects;
using Robust.Shared.Audio;

namespace Content.Server.GameTicking.Rules.Components;

[RegisterComponent, Access(typeof(SoulTieRuleSystem))]
public sealed partial class SoulTieRuleComponent : Component
{

    [DataField]
    public float TeleportDistance = 20f;

    [DataField]
    public SoundSpecifier? TeleportSound = new SoundPathSpecifier("/Audio/Vanilla/Effects/soulTie.ogg");

    [DataField]
    public EntityUid? First;

    [DataField]
    public TransformComponent? FirstTransform;

    [DataField]
    public EntityUid? Second;

    [DataField]
    public TransformComponent? SecondTransform;

}
