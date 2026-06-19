using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Content.Shared.Actions;
using Robust.Shared.Audio;

namespace Content.Shared.Vanilla.CompoundZ;

[RegisterComponent, NetworkedComponent]
public sealed partial class SuperComponent : Component
{
    [DataField]
    public SuperAbilityPrototype? Prototype { get; set; }

    [DataField]
    public EntityUid?[] Actions { get; set; } = Array.Empty<EntityUid?>();

    [DataField]
    public SoundSpecifier? BriefingSound = new SoundPathSpecifier("/Audio/Vanilla/Ambience/Antag/Super.ogg");

    [DataField]
    public SoundSpecifier? UnsuperedSound = new SoundPathSpecifier("/Audio/Vanilla/Ambience/Antag/Unsuper.ogg");
}

[Prototype]
public sealed partial class SuperAbilityPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = string.Empty;

    [DataField]
    public string Description = string.Empty;

    [DataField]
    public ComponentRegistry Components { get; private set; } = new();

    [DataField]
    public ComponentRegistry UnremovableComponents { get; private set; } = new();

    [DataField]
    public string[] Actions { get; private set; } = Array.Empty<string>();
}

public sealed class SuperBornEvent : EntityEventArgs
{
    public EntityUid Entity { get; }
}

public sealed class SuperLossEvent : EntityEventArgs
{
    public EntityUid Entity { get; }
}
