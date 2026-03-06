using Content.Shared.FixedPoint;
using Content.Shared.Damage;
using Robust.Shared.Network;

namespace Content.Shared.Vanilla.TDM;

[RegisterComponent]
public sealed partial class TDMMarkerComponent : Component
{
    [DataField]
    public EntityUid? RuleLink = null;
    public NetUserId UserId = default;

    [DataField]
    public bool Team = true;

    [DataField]
    public int TotalKills = 0;

    [DataField]
    public FixedPoint2 TotalDamage = new();

    [DataField]
    public EntityUid? Summoner = null;

    [DataField]
    public bool Summon = false;

    public DamageSpecifier Damage = new()
    {
        DamageDict = new()
        {
            { "Poison", 1200 }
        }
    };
}
