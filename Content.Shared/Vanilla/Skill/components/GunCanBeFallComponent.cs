using Robust.Shared.GameStates;

namespace Content.Shared.Vanilla.Skill
{
    [RegisterComponent]
    public sealed partial class GunCanBeFallComponent : Component
    {
        [DataField("RequiresRangeWeaponLevel")]
        public int RequiresRangeWeaponLevel { get; set; } = 1;

        [DataField("recoil")]
        public float recoil { get; set; } = 10f;

        
    }
}
