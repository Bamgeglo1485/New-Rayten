using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Content.Shared.Actions;
using Robust.Shared.Audio;

namespace Content.Shared.Vanilla.CompoundZ;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FlyerComponent : Component
{
    [DataField]
    public EntProtoId Action = "ActionFly";

    [DataField]
    public EntityUid? ActionEntity;

    [DataField, AutoNetworkedField]
    public bool IsFlying = false;

    [DataField, AutoNetworkedField]
    public float FlySpeedModifier = 3.0f;

    // Базовая скорость коллизии от которого зависит урон
    [DataField, AutoNetworkedField]
    public float MinCollisionSpeed = 5.0f;

    // Урон наносимый структурам от столкновения
    [DataField, AutoNetworkedField]
    public float StructuralDamage = 100.0f;

    // Урон наносимый пользователю от столкновения
    [DataField, AutoNetworkedField]
    public float UserBruteDamage = 1.0f;

    [DataField]
    public EntityUid?[] Actions { get; set; } = Array.Empty<EntityUid?>();

    [DataField]
    public SoundSpecifier? FlyedSound = new SoundPathSpecifier("/Audio/Vanilla/Effects/fly_up.ogg");

    [DataField]
    public SoundSpecifier? UnflyedSound = new SoundPathSpecifier("/Audio/Vanilla/Effects/fly_down.ogg");
}

public sealed partial class FlyActionEvent : InstantActionEvent;
