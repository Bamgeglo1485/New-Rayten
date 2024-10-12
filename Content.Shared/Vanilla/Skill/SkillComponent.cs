using Robust.Shared.GameStates;

namespace Content.Shared.Vanilla.Skill
{
    [RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
    public sealed partial class SkillComponent : Component
    {
        [DataField("MedicineLevel"), AutoNetworkedField]
        public int MedicineLevel { get; set; } = 0;

        [DataField("ChemistryLevel"), AutoNetworkedField]
        public int ChemistryLevel { get; set; } = 0;

        [DataField("RangeWeaponLevel"), AutoNetworkedField]
        public int RangeWeaponLevel { get; set; } = 0;
    }
}
