using Content.Server.Projectiles;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction.Events;
using Content.Shared.Projectiles;
using Content.Shared.Teleportation.Components;
using Content.Shared.Teleportation.Systems;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Components;

namespace Content.Server.Teleportation;

public sealed class PortalGunSystem : EntitySystem
{
    [Dependency] private readonly ProjectileSystem _projectile = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutionSystem = default!;
    [Dependency] private readonly GunSystem _gunSystem = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<PortalGunComponent, OnEmptyGunShotEvent>(OnEmptyGunShot);
    }

    private void OnEmptyGunShot(EntityUid uid, PortalGunComponent component, ref OnEmptyGunShotEvent args)
    {
        if (!_solutionSystem.TryGetSolution(uid, component.SolutionName, out var solution, out var solutionComp))
            return;

        if (!TryComp<BatteryWeaponFireModesComponent>(uid, out var fireModes) ||
            fireModes.FireModes.Count == 0)
            return;

        var currentMode = fireModes.FireModes[fireModes.CurrentFireMode];
        var amountToRemove = FixedPoint2.New(currentMode.FireCost);

        if (solutionComp.GetTotalPrototypeQuantity(component.ReagentName) < amountToRemove ||
            _solutionSystem.RemoveReagent(solution.Value, component.ReagentName, amountToRemove) <= FixedPoint2.Zero)
        {
            return;
        }
        
        var projectile = Spawn(currentMode.Prototype, _transform.GetMapCoordinates(uid));

        if (TryComp<ProjectileComponent>(projectile, out var projectileComp))
        {
            projectileComp.Shooter = args.User;
        }

        if (TryComp<PhysicsComponent>(projectile, out var physics))
        {
            var direction = _transform.GetWorldRotation(args.User).ToWorldVec();
            _physics.SetLinearVelocity(projectile, direction * component.ProjectileVelocity, body: physics);
        }
    }
}
