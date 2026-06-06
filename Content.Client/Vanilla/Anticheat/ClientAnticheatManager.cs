using Content.Shared.Vanilla.Anticheat;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Movement.Components;
using Robust.Shared.Timing;
using Robust.Client.Player;
namespace Content.Client.Vanilla.Anticheat;

public sealed partial class ClientAnticheatManager : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    private TimeSpan _nextCheck = TimeSpan.Zero;

    public override void Initialize()
    {
        SubscribeLocalEvent<RequestShootEvent>(OnAimBotBait);
    }

    private void OnAimBotBait(RequestShootEvent msg, EntitySessionEventArgs args)
    {
        ReportCheat($"Пункт 6. Нечестная игра. Модификация клиента игры, использование читов.");
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
            ReportCheat("Пункт 6. Нечестная игра. Модификация клиента игры, использование читов.");
            return;
        }

        if (TryComp<ContentEyeComponent>(playerEnt.Value, out var contentEye) && !contentEye.NetSyncEnabled)
        {
            ReportCheat("Пункт 6. Нечестная игра. Модификация клиента игры, использование читов.");
            return;
        }
    }

    private void ReportCheat(string reason, bool withBan = true)
    {
        var msg = new SuspiciousClientEvent(reason, withBan);
        RaiseNetworkEvent(msg);
    }
}
