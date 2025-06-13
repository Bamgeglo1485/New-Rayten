using Content.Shared.FixedPoint;
using Content.Shared.Damage;

namespace Content.Shared.Vanilla.TDMRoundEnd;

[RegisterComponent]
public sealed partial class TDMMarkerComponent : Component
{
    [DataField]
    public EntityUid? RuleLink = null;

    [DataField]
    public bool Team = true;

    [DataField]
    public int TotalKills = 0;
    
    [DataField]
    public FixedPoint2 TotalDamage = new();

    public DamageSpecifier Damage = new()
    {
        DamageDict = new()
        {
            { "Poison", 1200 }
        }
    };
}
