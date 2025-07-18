using Content.Shared.Vanilla.Anticheat;
using Content.Shared.Ghost;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Interaction;
using Content.Shared.Revenant.Components;
using Content.Shared.Examine;
using Content.Shared.Movement.Components;
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
        [Dependency] private readonly IPlayerManager _playerManager = default!;
        private TimeSpan _nextCheck = TimeSpan.Zero;

        public override void Initialize()
        {
            SubscribeLocalEvent<RequestShootEvent>(OnAimBotBait);
        }

        private void OnAimBotBait(RequestShootEvent msg, EntitySessionEventArgs args)
        {
            ReportCheat($"Обнаружен аимбот");
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            var now = _timing.CurTime;

            if (now < _nextCheck)
                return;

            _nextCheck = now + TimeSpan.FromSeconds(5);
            CheckDrawFovFlag();
        }

        private void CheckDrawFovFlag()
        {
            var session = _playerManager.LocalSession;

            if (session == null)
                return;

            var playerEnt = session.AttachedEntity;

            if (playerEnt == null)
                return;


            if (TryComp<EyeComponent>(playerEnt.Value, out var eye) && !eye.NetSyncEnabled)
            {
                ReportCheat("Отключена синхронизация EyeComponent");
                return;
            }

            if (TryComp<ContentEyeComponent>(playerEnt.Value, out var contentEye) && !contentEye.NetSyncEnabled)
            {
                ReportCheat("Отключена синхронизация ContentEyeComponent");
                return;
            }
        }

        private void ReportCheat(string reason)
        {
            var msg = new SuspiciousClientEvent(reason);
            RaiseNetworkEvent(msg);
        }
    }
}
