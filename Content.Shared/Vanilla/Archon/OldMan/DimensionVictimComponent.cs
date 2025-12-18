using Content.Shared.FixedPoint;
using Content.Shared.Damage;

using Robust.Shared.Audio;

namespace Content.Shared.Vanilla.Archon.OldMan;

[RegisterComponent]
public sealed partial class DimensionVictimComponent : Component
{

    [DataField]
    public SoundSpecifier? DimensionEscapeSound = new SoundPathSpecifier("/Audio/Vanilla/Effects/Archon/106laugh2.ogg");

    [DataField]
    public SoundSpecifier? DamageSound = new SoundPathSpecifier("/Audio/Vanilla/Effects/Archon/106decay.ogg");

    [DataField]
    public SoundSpecifier? DimensionAmbient = new SoundPathSpecifier("/Audio/Vanilla/Ambience/106dimension.ogg");

    [DataField]
    public string EscapeEffect = "Lusha";

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan DamageInterval = TimeSpan.FromSeconds(40);

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan NextDamage;

    [DataField]
    [ViewVariables(VVAccess.ReadWrite)]
    public DamageSpecifier Damage = new()
    {
        DamageDict = new Dictionary<string, FixedPoint2>
        {
            ["Cellular"] = 5,
            ["Caustic"] = 5
        }
    };

    [DataField]
    [ViewVariables(VVAccess.ReadWrite)]
    public DamageSpecifier FinalDamage = new()
    {
        DamageDict = new Dictionary<string, FixedPoint2>
        {
            ["Cellular"] = 30,
            ["Caustic"] = 80
        }
    };
}
