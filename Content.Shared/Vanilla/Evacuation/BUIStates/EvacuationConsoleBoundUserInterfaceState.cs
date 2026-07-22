using Robust.Shared.Serialization;

namespace Content.Shared.Vanilla.Evacuation.BUIStates;

[Serializable, NetSerializable]
public sealed class EvacuationConsoleBoundUserInterfaceState : BoundUserInterfaceState
{
}

[Serializable, NetSerializable]
public enum EvacuationConsoleUiKey : byte
{
    Key,
}
