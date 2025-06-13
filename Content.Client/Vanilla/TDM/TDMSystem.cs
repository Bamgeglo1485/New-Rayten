using Content.Shared.Vanilla.TDM;
using Robust.Shared.Timing;

namespace Content.Client.Vanilla.TDM;

public sealed class TDMSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    public event Action<TimeSpan, int>? TDMInfoUpdated;

    private int Playercount = 0;
    private TimeSpan TimeToStartTDM = TimeSpan.FromSeconds(-1);
    private bool CanJoin = false;

    public TimeSpan NextUpdate;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<TDMInformation>(RefreshInformation);
    }

    private void RefreshInformation(TDMInformation msg, EntitySessionEventArgs args)
    {
        Playercount = msg.PlayerCount;
        TimeToStartTDM = msg.TimeToStart;
        CanJoin = msg.CanJoin;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var currentTime = _gameTiming.CurTime;

        if (currentTime < NextUpdate)
            return;

        NextUpdate = currentTime + TimeSpan.FromSeconds(1);

        if (!CanJoin)
        {
            TDMInfoUpdated?.Invoke(TimeSpan.FromSeconds(-1), Playercount);
            return;
        }

        TDMInfoUpdated?.Invoke(TimeToStartTDM, Playercount);

        if (Playercount < 2)
            return;


        TimeToStartTDM -= TimeSpan.FromSeconds(1);
    }

    /// <summary>
    /// Отправляем запрос на участие в тдме
    /// </summary>
    public void TPMeToArena()
    {
        var msg = new TPMeToTDMEvent();
        RaiseNetworkEvent(msg);
    }
}
