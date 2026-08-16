using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;
using Content.Shared.Projectiles;
using Content.Shared.Vanilla.Teleportation.Components;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Vanilla.Teleportation;
using Content.Shared.Maps;
using Content.Shared.Popups;
using Content.Shared.Verbs;

using Content.Server.Administration;

using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Player;
using Robust.Shared.Map;

using Robust.Server.Audio;

using System.Numerics;

namespace Content.Server.Vanilla.Teleportation;

public sealed partial class PortalGunSystem : EntitySystem
{
    [Dependency] private SharedSolutionContainerSystem _solutionSystem = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private QuickDialogSystem _quickDialog = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedMapSystem _mapSystem = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<PortalGunComponent, PortalGunShootEvent>(AttemptShoot);

        SubscribeLocalEvent<PortalGunComponent, GetVerbsEvent<ActivationVerb>>(AddVerb);
    }

    private void AttemptShoot(EntityUid uid, PortalGunComponent component, ref PortalGunShootEvent args)
    {
        if (!_solutionSystem.TryGetSolution(uid, component.SolutionName, out var solution, out var solutionComp))
            return;

        if (!TryComp<BatteryWeaponFireModesComponent>(uid, out var fireModes) ||
            fireModes.FireModes.Count == 0)
            return;

        var currentMode = fireModes.FireModes[fireModes.CurrentFireMode];

        if (currentMode.Prototype == component.CoordinatedPortalProjectile &&
            (component.SavedCoordinates.Count <= component.Index || component.SavedCoordinates[component.Index] == null))
        {
            _audio.PlayPvs(component.EmptyShotSound, uid);
            _popup.PopupEntity("Нет сохраненных координат для этой позиции!", uid, args.User);
            return;
        }

        var amountToRemove = FixedPoint2.New(currentMode.FireCost);

        if (solutionComp.GetTotalPrototypeQuantity(component.ReagentName) < amountToRemove ||
            _solutionSystem.RemoveReagent(solution.Value, component.ReagentName, amountToRemove) <= FixedPoint2.Zero)
        {
            _audio.PlayPvs(component.EmptyShotSound, uid);
            return;
        }

        var projectile = Spawn(currentMode.Prototype, _transform.GetMapCoordinates(uid));
        _audio.PlayPvs(component.ShotSound, uid);

        if (TryComp<SpawnCoordinatedPortalOnTriggerComponent>(projectile, out var cordPortalComp) &&
            component.SavedCoordinates.Count > component.Index &&
            component.SavedCoordinates[component.Index] != null)
        {
            cordPortalComp.Coordinates = component.SavedCoordinates[component.Index];
        }

        if (TryComp<ProjectileComponent>(projectile, out var projectileComp))
            projectileComp.Shooter = args.User;

        if (TryComp<PhysicsComponent>(projectile, out var physics) && TryComp<GunComponent>(uid, out var gun))
        {
            var userXform = Transform(args.User);
            var direction = userXform.LocalRotation.ToWorldVec();
            direction = direction.Normalized();

            _physics.SetLinearVelocity(projectile, direction * gun.ProjectileSpeed, body: physics);
        }
    }

    private void AddVerb(EntityUid uid, PortalGunComponent comp, GetVerbsEvent<ActivationVerb> args)
    {
        if (!TryComp(args.User, out ActorComponent? actor))
            return;

        if (!comp.CanTypeCoordinates)
            return;

        if (!args.CanInteract)
            return;

        var verb = new ActivationVerb
        {
            Text = "Ввести координаты",
            Act = () =>
            {
                EnsureCoordinatesListSize(comp);

                var x = 0;
                var y = 0;

                var currentCoord = comp.SavedCoordinates[comp.Index];
                if (currentCoord != null)
                {
                    x = (int)currentCoord.Value.Position.X;
                    y = (int)currentCoord.Value.Position.Y;
                }

                var xVal = x;
                var yVal = y;

                _quickDialog.OpenDialog(actor.PlayerSession, "Ввести координаты",
                    $"Введите X координату (текущая: {xVal})", (string message) =>
                    {
                        if (!int.TryParse(message, out var xMes))
                            return;

                        if (xMes > 1000)
                            xMes = 1000;
                        if (xMes < -1000)
                            xMes = -1000;

                        xVal = xMes;
                        _audio.PlayPvs(comp.SaveCoordinatesSound, uid);

                        SaveCoordinates(uid, comp, comp.Index, xVal, yVal);
                    });

                _quickDialog.OpenDialog(actor.PlayerSession, "Ввести координаты",
                    $"Введите Y координату (текущая: {yVal})", (string message) =>
                    {
                        if (!int.TryParse(message, out var yMes))
                            return;

                        if (yMes > 1000)
                            yMes = 1000;
                        if (yMes < -1000)
                            yMes = -1000;

                        yVal = yMes;
                        _audio.PlayPvs(comp.SaveCoordinatesSound, uid);

                        SaveCoordinates(uid, comp, comp.Index, xVal, yVal);
                    });
            },
        };

        args.Verbs.Add(verb);
    }

    private void EnsureCoordinatesListSize(PortalGunComponent comp)
    {
        while (comp.SavedCoordinates.Count <= comp.Index)
        {
            comp.SavedCoordinates.Add(null);
        }
    }

    private void SaveCoordinates(EntityUid uid, PortalGunComponent comp, int index, int x, int y)
    {
        while (comp.SavedCoordinates.Count <= index)
        {
            comp.SavedCoordinates.Add(null);
        }

        var mapId = _transform.GetMapCoordinates(uid).MapId;
        comp.SavedCoordinates[index] = new MapCoordinates(new Vector2(x, y), mapId);

        _popup.PopupEntity($"Сохранены координаты для ячейки {index}: X={x}, Y={y}", uid);
        _audio.PlayPvs(comp.SaveCoordinatesSound, uid);

        Dirty(uid, comp);
    }
}
