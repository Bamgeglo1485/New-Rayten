using Content.Shared.FixedPoint;
using Content.Shared.Damage;
using Content.Shared.StatusIcon;
using Robust.Shared.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Player;

namespace Content.Shared.Vanilla.Games.TTT;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TTTMarkerComponent : Component
{
    [DataField]
    public EntityUid? RuleLink = null;

    [DataField, AutoNetworkedField]
    public string Name = "Подозрительный человек";

    [DataField, AutoNetworkedField]
    public TTTRole Role = TTTRole.await;

    [DataField]
    public int TotalKills = 0;

    [DataField]
    public ICommonSession Session;

    public DamageSpecifier Damage = new()
    {
        DamageDict = new()
        {
            { "Poison", 1200 }
        }
    };

    [DataField("DecStatusIcon", customTypeSerializer: typeof(PrototypeIdSerializer<FactionIconPrototype>))]
    public string DecStatusIcon = "TTTDetectiveFaction";
    public string GetRoleName()
    {
        return Role switch
        {
            TTTRole.inocent => "Невиновный",
            TTTRole.traitor => "Предатель",
            TTTRole.detective => "Детектив",
            TTTRole.await => "Ожидание",
            _ => "Неизвестная роль"
        };
    }
}

public enum TTTRole : byte
{
    await = 0,
    inocent = 1,
    detective = 2,
    traitor = 3,
}

[RegisterComponent, NetworkedComponent]
public sealed partial class ShowTTTTraitorsComponent : Component {}

[RegisterComponent, NetworkedComponent]
public sealed partial class ShowTTTDetectiveIconsComponent : Component {}

[RegisterComponent, NetworkedComponent]
public sealed partial class TTTTRAITORComponent : Component
{
    [DataField("syndStatusIcon", customTypeSerializer: typeof(PrototypeIdSerializer<FactionIconPrototype>))]
    public string SyndStatusIcon = "SyndicateFaction";
}