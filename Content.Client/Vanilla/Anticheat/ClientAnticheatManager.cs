using Content.Shared.Vanilla.Anticheat;
using Content.Shared.Ghost;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Interaction;
using Content.Shared.Revenant.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Log;
using Robust.Shared.Network;
using Robust.Shared.Timing;
using Robust.Shared.Player;
using Robust.Client.Player;
using Robust.Client.Graphics;
using Robust.Client.GameObjects;
using System;
using System.Collections.Generic;

namespace Content.Client.Vanilla.Anticheat
{
    public sealed class ClientAnticheatManager : EntitySystem
    {
        [Dependency] private readonly IGameTiming _timing = default!;
        [Dependency] private readonly IOverlayManager _overlayManager = default!;
        [Dependency] private readonly INetManager _netManager = default!;

        [Dependency] private readonly IEntityManager _entMan = default!;
        [Dependency] private readonly IPlayerManager _playerManager = default!;
        [Dependency] private readonly IEyeManager _eyeManager = default!;
        [Dependency] private readonly ILightManager _light = default!;
        [Dependency] private readonly SharedInteractionSystem _interactionSystem = default!;
        /// <summary>
        /// При наличии 5+ читпоинтов отправляется репорт
        /// </summary>
        private int cheatpoints = 0;
        private TimeSpan _nextCheck = TimeSpan.Zero;

        public override void Initialize()
        {
            SubscribeLocalEvent<RequestShootEvent>(OnAimBotBait);
        }

        private void CheckForVisibleTraps()
        {
            var session = _playerManager.LocalSession;
            if (session == null || session.AttachedEntity is not { } playerEnt)
                return;

            if (HasComp<GhostComponent>(playerEnt) || HasComp<RevenantComponent>(playerEnt) || HasComp<AntiCheatIgnoreComponent>(playerEnt))
                return;

            // Перебираем все ловушки
            foreach (var trap in EntityQuery<AnticheatTrapComponent>())
            {
                var trapUid = trap.Owner;

                // Игрок не должен видеть ловушку
                if (!IsEntityVisibleToPlayer(trapUid, playerEnt))
                    cheatpoints += 5;
            }
        }

        private bool IsEntityVisibleToPlayer(EntityUid target, EntityUid player)
        {
            // Проверяем, что оба валидны
            if (!_entMan.EntityExists(target) || !_entMan.EntityExists(player))
                return false;

            if (!TryComp<TransformComponent>(target, out var targetXform))
                return false;

            if (!TryComp<TransformComponent>(player, out var playerXform))
                return false;

            // Проверяем, что они на одной карте
            if (targetXform.MapID != playerXform.MapID)
                return false;

            // Проверяем преграды
            return _interactionSystem.InRangeUnobstructed(player, target, range: 16.0f);
        }

        private void OnAimBotBait(RequestShootEvent msg, EntitySessionEventArgs args)
        {
            ReportCheat($"Обнаружен аимбот");
        }


        // public override void Update(float frameTime)
        // {
        //     base.Update(frameTime);

        //     var now = _timing.CurTime;

        //     if (now < _nextCheck)
        //         return;

        //     if (cheatpoints >= 5)
        //     {
        //         ReportCheat($"Рейтинг подозрения: {cheatpoints}");
        //         return;
        //     }

        //     _nextCheck = now + TimeSpan.FromSeconds(5);
        //     CheckForVisibleTraps();
        //     // CheckDrawFovFlag();
        // }

        private void CheckDrawFovFlag()
        {
            // Получаем сессию локального игрока
            var session = _playerManager.LocalSession;
            if (session == null)
                return;

            var playerEnt = session.AttachedEntity;
            if (playerEnt == null)
                return;

            // Игрок не должен быть призраком
            if (HasComp<GhostComponent>(playerEnt.Value))
                return;
            if (HasComp<RevenantComponent>(playerEnt.Value))
                return;

            if (!_eyeManager.CurrentEye.DrawFov)
            {
                cheatpoints += 1;
            }

            if (!_light.Enabled)
            {
                cheatpoints += 1;
            }

            if (!_light.DrawShadows)
            {
                cheatpoints += 1;
            }
        }

        private void ReportCheat(string reason)
        {
            var msg = new SuspiciousClientEvent(reason);
            RaiseNetworkEvent(msg);
            cheatpoints = 0;
        }
    }
}
