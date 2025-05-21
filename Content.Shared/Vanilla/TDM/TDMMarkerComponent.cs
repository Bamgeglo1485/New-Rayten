using Content.Shared.FixedPoint;
using Content.Shared.Damage;
namespace Content.Shared.Vanilla.TDMRoundEnd;

[RegisterComponent]
public sealed partial class TDMMarkerComponent : Component
{
    [DataField("team")]
    public bool Team = true; //1-red 0-blue

    public int TotalKills = 0;
    public int TotalASSIST = 0;
    public FixedPoint2 TotalDamage = new();
    [DataField("damage")] public DamageSpecifier Damage = new()
    {
        DamageDict = new ()
        {
            { "Poison", 1200 }
        }
    };
}
