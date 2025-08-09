using Content.Shared.FixedPoint;
using Content.Shared.Damage;
using Robust.Shared.Player;
namespace Content.Shared.Vanilla.Games.TTT;

[RegisterComponent]
public sealed partial class TTTMarkerComponent : Component
{
    [DataField]
    public EntityUid? RuleLink = null;

    [DataField]
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
    //только для отображения
    [DataField]
    public int Karma = 0;
}
public enum TTTRole : byte
{
    await = 0,
    inocent = 1,
    detective = 2,
    traitor = 3,
}