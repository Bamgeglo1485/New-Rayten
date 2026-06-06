using Content.Shared.Vanilla.Weapons.Ranged;
using Content.Shared.CombatMode;
using Content.Client.Gameplay;
using Robust.Shared.Input;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Client.State;
using Robust.Client.Graphics;
using Robust.Client.Input;

namespace Content.Client.Vanilla.Weapons.Ranged;

public sealed partial class MicroHIDSystem : SharedMicroHIDSystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private InputSystem _inputSystem = default!;
    [Dependency] private IEyeManager _eyeManager = default!;
    [Dependency] private IInputManager _inputManager = default!;
    [Dependency] private IStateManager _stateManager = default!;
    [Dependency] private SharedCombatModeSystem _combatMode = default!;
    public override void Initialize()
    {
        base.Initialize();
        UpdatesOutsidePrediction = true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!Timing.IsFirstTimePredicted)
            return;

        var entityNull = _player.LocalEntity;

        if (entityNull == null)
            return;

        var entity = entityNull.Value;

        if (!TryGetChargedWeapon(entity, out var weaponUid, out var chargedcomp))
            return;
        var useDown = _inputSystem.CmdStates.GetState(EngineKeyFunctions.Use);
        var incombat = _combatMode.IsInCombatMode(entity);

        //Лкм отжали во время стрельбы или вышел из боевого режима во время стрельбы
        if (chargedcomp.IsShooting && (!incombat || useDown != BoundKeyState.Down))
        {
            RaisePredictiveEvent(new WeaponChargeEvent(GetNetEntity(weaponUid), false));
            return;
        }

        //Лкм нажали без стрельбы
        if (!chargedcomp.IsShooting && useDown == BoundKeyState.Down && incombat)
        {
            RaisePredictiveEvent(new WeaponChargeEvent(GetNetEntity(weaponUid), true));
            return;
        }

        //настало время шмалять
        if (chargedcomp.IsShooting && Timing.CurTime >= chargedcomp.NextShootAt)
        {
            if (_stateManager.CurrentState is GameplayStateBase screen)
            {
                var mousePos = _eyeManager.PixelToMap(_inputManager.MouseScreenPosition);
                var target = screen.GetClickedEntity(mousePos);
                RaisePredictiveEvent(new WeaponChargeShootRequestEvent(GetNetEntity(weaponUid), GetNetEntity(target)));
            }
        }
    }
}
