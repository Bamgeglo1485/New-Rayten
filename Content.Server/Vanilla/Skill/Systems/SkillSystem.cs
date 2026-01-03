using Content.Server.Hands.Systems;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Vanilla.Skill;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs.Components;
using Content.Shared.Chemistry.Components;

namespace Content.Server.Vanilla.Skill;

public sealed partial class SkillSystem : SharedSkillSystem
{
    const float HEADSHOTCHANCE = 0.3f;
    [Dependency] private readonly SharedStaminaSystem _stamina = default!;
    [Dependency] private readonly HandsSystem _hands = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MobStateComponent, DamageChangedEvent>(OnHit);
        SubscribeLocalEvent<GunCanBeFallComponent, GunShotEvent>(RangeWeaponFalldownOnShoot);
        SubscribeLocalEvent<SolutionContainerVisualsComponent, MapInitEvent>(OnMapInit);
    }
}