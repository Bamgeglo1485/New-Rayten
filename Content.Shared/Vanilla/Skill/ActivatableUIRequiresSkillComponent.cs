using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Vanilla.Skill
{
    [RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
    public sealed partial class ActivatableUIRequiresSkillComponent : Component
    {
        [DataField("RequiresChemistryLevel"), AutoNetworkedField]
        public int RequiresChemistryLevel { get; set; } = 0;

        [DataField("RequiresMedicineLevel"), AutoNetworkedField]
        public int RequiresMedicineLevel { get; set; } = 0;
    }


}
