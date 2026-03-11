using Content.Shared.Damage;
using Content.Shared.Trigger.Components.Effects;
using Robust.Shared.GameStates;
namespace Content.Shared.Vanilla.Games.TTT.Items;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TTTExplodeOnTriggerComponent : BaseXOnTriggerComponent
{
    [DataField, AutoNetworkedField]
    public float Range = 15f;
    [DataField, AutoNetworkedField]
    public DamageSpecifier Damage = new()
    {
        DamageDict = new()
        {
            ["Blunt"] = 100
        }
    };
}
