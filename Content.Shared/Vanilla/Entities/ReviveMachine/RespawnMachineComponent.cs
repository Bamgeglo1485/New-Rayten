using Robust.Shared.Audio;
using Content.Shared.DeviceLinking;
using Robust.Shared.Prototypes;

namespace Content.Shared.Vanilla.Entities.RespawnMachine;

[RegisterComponent]
public sealed partial class RespawnMachineComponent : Component
{
    [DataField]
    public float RespawnDelay = 600f;

    [DataField]
    public SoundSpecifier? RespawnSound = new SoundPathSpecifier("/Audio/Vanilla/Effects/revive.ogg");

    [ViewVariables]
    public EntityUid? InventoryTaker;

    [DataField]
    public ProtoId<SourcePortPrototype> LinkingPort = "RespawnMachineSender";

    [DataField]
    public float WrongBodyChance = 0.01f;
}
