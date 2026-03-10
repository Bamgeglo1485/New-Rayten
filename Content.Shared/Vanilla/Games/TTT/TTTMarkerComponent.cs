using Content.Shared.StatusIcon;
using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Player;

namespace Content.Shared.Vanilla.Games.TTT;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NameOverlayComponent : Component
{
    [DataField, AutoNetworkedField]
    public string Name = "Турель";
    [DataField]
    public string OldName = "НЕИЗВЕСТНЫЙ";

    [DataField, AutoNetworkedField]
    public Color NameColor = Color.Green;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TTTMarkerComponent : Component
{
    [DataField]
    public EntityUid RuleLink;

    [DataField, AutoNetworkedField]
    public TTTRole Role = TTTRole.Await;

    [DataField]
    public int TotalKills = 0;

    [DataField]
    public ICommonSession Session;
    [DataField]
    public bool TeamKiller = false;

    [DataField("DecStatusIcon", customTypeSerializer: typeof(PrototypeIdSerializer<FactionIconPrototype>))]
    public string DecStatusIcon = "TTTDetectiveFaction";

    public Color GetColor()
    {
        return Role switch
        {
            TTTRole.Inocent => Color.Green,
            TTTRole.Traitor => Color.Red,
            TTTRole.Detective => Color.DodgerBlue,
            _ => Color.Green
        };
    }



    /// <summary>
    /// туду в loc
    /// </summary>
    public string GetRoleName()
    {
        return Role switch
        {
            TTTRole.Inocent => "Невиновный",
            TTTRole.Traitor => "Предатель",
            TTTRole.Detective => "Детектив",
            _ => "...ээ?"
        };
    }
    /// <summary>
    /// туду в loc
    /// </summary>
    public string GetUIRoleName()
    {
        return Role switch
        {
            TTTRole.Inocent => "Невиновным",
            TTTRole.Traitor => "Предателем",
            TTTRole.Detective => "Детективом",
            _ => "....Кем?"
        };
    }
}

public enum TTTRole : byte
{
    Await = 0,
    Inocent = 1,
    Detective = 2,
    Traitor = 3,
}

[RegisterComponent, NetworkedComponent]
public sealed partial class ShowTTTTraitorsComponent : Component { }

[RegisterComponent, NetworkedComponent]
public sealed partial class ShowTTTDetectiveIconsComponent : Component { }

[RegisterComponent, NetworkedComponent]
public sealed partial class TTTTRAITORComponent : Component
{
    [DataField("syndStatusIcon", customTypeSerializer: typeof(PrototypeIdSerializer<FactionIconPrototype>))]
    public string SyndStatusIcon = "SyndicateFaction";
}
public sealed partial class TTTDisguiserActionEvent : InstantActionEvent
{
}
