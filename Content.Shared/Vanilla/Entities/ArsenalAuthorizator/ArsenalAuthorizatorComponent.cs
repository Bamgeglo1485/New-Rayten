using Robust.Shared.Serialization;
using Robust.Shared.GameStates;
namespace Content.Shared.Vanilla.Entities.ArsenalAuthorizator;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ArsenalAuthorizatorComponent : Component
{
    /// <summary>
    /// открыто?
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool IsOpen = false;
}


[Serializable, NetSerializable]
public sealed class ArsenalAuthorizatorOpenMessage : BoundUserInterfaceMessage
{
    public string ReasonId;

    public ArsenalAuthorizatorOpenMessage(string reasonId)
    {
        ReasonId = reasonId;
    }
}


[Serializable, NetSerializable]
public sealed class ArsenalAuthorizatorBoundInterfaceState : BoundUserInterfaceState
{
    public ArsenalAuthorizatorBoundInterfaceState()
    {
    }
}

public enum ArsenalAuthorizatorState : byte
{
    Open = 0,
    Close = 1,
}

[Serializable, NetSerializable]
public enum ArsenalAuthorizatorVisuals : byte
{
    ControlPanel,
}

[Serializable, NetSerializable]
public enum ArsenalAuthorizatorUiKey : byte
{
    Key,
}
