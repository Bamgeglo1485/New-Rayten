using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared.Vanilla.Games.TTT.Items.Radar;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TTTRadarComponent : Component
{
    [ViewVariables, AutoNetworkedField]
    public List<RadarBlip> TrackedEntities = new();
    [AutoNetworkedField]
    public TimeSpan NextScan;

    [ViewVariables]
    public bool TraitorRadar = false;
}

[Serializable, NetSerializable]
public struct RadarBlip(NetCoordinates coords, Color color)
{
    public NetCoordinates Coords = coords;
    public Color Color = color;
}

[Serializable, NetSerializable]
public sealed class TTTRadarInterfaceState : BoundUserInterfaceState
{
}
