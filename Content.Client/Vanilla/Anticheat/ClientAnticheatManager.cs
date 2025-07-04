using Content.Shared.Vanilla.Anticheat;
using Content.Shared.Ghost;
using Content.Shared.Weapons.Ranged.Events;
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

        private TimeSpan _nextCheck = TimeSpan.Zero;
        private bool _alreadyReported = false;

        public override void Initialize()
        {
            SubscribeLocalEvent<RequestShootEvent>(OnAimBotBait);
        }
        private void OnAimBotBait(RequestShootEvent msg, EntitySessionEventArgs args)
        {
              ReportCheat("Aimbot_trigger");
        }


        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            if (_alreadyReported)
                return;

            var now = _timing.CurTime;

            if (now < _nextCheck)
                return;

            _nextCheck = now + TimeSpan.FromSeconds(2);
            // CheckForVisibleTraps();
            CheckDrawFovFlag();
        }

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

            if (!_eyeManager.CurrentEye.DrawFov)
            {
                ReportCheat("drawfov_disabled");
            }
            if (!_light.Enabled)
            {
                ReportCheat("lights_disabled");
            }
            if (!_light.DrawShadows)
            {
                ReportCheat("lights_disabled");
            }
        }

        // private void CheckForVisibleTraps()
        // {
        //     var eye = _eyeManager.CurrentEye;

        //     var query = _entMan.EntityQueryEnumerator<TrapMarkerComponent, TransformComponent>();
        //     while (query.MoveNext(out var uid, out var trap, out var xform))
        //     {
        //         // Сущность находится на другом слое — пропускаем
        //         if (xform.MapID != eye.MapId)
        //             continue;

        //         // Проверяем: видит ли глаз эту ловушку
        //         var eyePos = eye.Position;
        //         var trapPos = xform.MapPosition.Position;

        //         // Достаточно ли близко (в радиусе 20 тайлов)
        //         if ((trapPos - eyePos).Length > 20)
        //             continue;

        //         // Проверяем — существует ли Sprite, и он видим
        //         if (!TryComp<SpriteComponent>(uid, out var sprite))
        //             continue;

        //         if (!sprite.Visible)
        //             continue;

        //         // Важно: в FoV она НЕ должна быть — проверим
        //         // Мы не можем гарантировать, что в FoV напрямую,
        //         // но если она отображается, хотя НЕ должна —
        //         // это чит.
        //         if (sprite.Owner.IsClientSide()) // необязательно, но вдруг
        //             continue;

        //         // Ловушка отрисовывается, хотя должна быть в тумане — это чит
        //         ReportCheater("trap_visible_in_fog");
        //         break;
        //     }
        // }

        private void ReportCheat(string reason)
        {
            var msg = new SuspiciousClientEvent(reason);

            RaiseNetworkEvent(msg);

            _alreadyReported = true;
        }
    }
}
