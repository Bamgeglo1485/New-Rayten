using Robust.Shared.GameStates;
namespace Content.Shared.Vanilla.Skill;

[RegisterComponent]
public sealed partial class MeleeTrainerComponent : Component
{
    [DataField("ExpPerHit")]
    public int ExpPerHit { get; set; } = 3;

    [DataField("skillType")]
    public skillType SkillType { get; set; } = skillType.MeleeWeapon;
}

