using Robust.Shared.Audio;

namespace Content.Server.GameTicking.Rules.Components;

[RegisterComponent, Access(typeof(InstantRespawnRuleSystem))]
public sealed partial class InstantRespawnRuleComponent : Component
{
    [DataField]
    public SoundSpecifier? RespawnSound = new SoundPathSpecifier("/Audio/Vanilla/Effects/bell.ogg");
}
