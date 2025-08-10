using Content.Shared.Vanilla.TDM;
using Robust.Shared.Timing;
using Content.Shared.GameTicking;
using Content.Client.Vanilla.TDM.UI;

namespace Content.Client.Vanilla.TDM;

public sealed class TDMSystem : EntitySystem
{
    private AcceptTDMWindow? _tdmWindow;

    [Dependency] private readonly IGameTiming _gameTiming = default!;
    public event Action<TimeSpan, int>? TDMInfoUpdated;
    public event Action<TimeSpan, int>? TTTInfoUpdated;

    private int PlayercountTDM = 0;
    private int PlayercountTTT = 0;

    private TimeSpan TimeToStartTDM = TimeSpan.FromSeconds(-1);
    private TimeSpan TimeToStartTTT = TimeSpan.FromSeconds(-1);

    private bool CanJoinTDM = false;
    private bool CanJoinTTT = false;

    public TimeSpan NextUpdate;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<TDMInformation>(RefreshTDMInformation);
        SubscribeNetworkEvent<TTTInformation>(RefreshTTTInformation);
        SubscribeNetworkEvent<RoundEndMessageEvent>(OnRoundEndMessage);
    }

    private void OnRoundEndMessage(RoundEndMessageEvent ev)
    {
        if (_tdmWindow != null && _tdmWindow.IsOpen)
            return;

        _tdmWindow = new AcceptTDMWindow();

        _tdmWindow.AcceptButton.OnPressed += _ =>
        {
            _tdmWindow.Dispose();
            _tdmWindow = null;
            TPMeToTDM();
        };

        _tdmWindow.DenyButton.OnPressed += _ =>
        {
            _tdmWindow.Dispose();
            _tdmWindow = null;
        };

        _tdmWindow.OpenCentered();
    }

    private void RefreshTDMInformation(TDMInformation msg, EntitySessionEventArgs args)
    {
        PlayercountTDM = msg.PlayerCount;
        TimeToStartTDM = msg.TimeToStart;
        CanJoinTDM = msg.CanJoin;
    }
    private void RefreshTTTInformation(TTTInformation msg, EntitySessionEventArgs args)
    {
        PlayercountTTT = msg.PlayerCount;
        TimeToStartTTT = msg.TimeToStart;
        CanJoinTTT = msg.CanJoin;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var currentTime = _gameTiming.CurTime;

        if (currentTime < NextUpdate)
            return;
        TDMCHECK();
        TTTCHECK();
        NextUpdate = currentTime + TimeSpan.FromSeconds(1);
    }

    private void TDMCHECK()
    {
        if (!CanJoinTDM)
        {
            TDMInfoUpdated?.Invoke(TimeSpan.FromSeconds(-1), PlayercountTDM);
            return;
        }
        TDMInfoUpdated?.Invoke(TimeToStartTDM, PlayercountTDM);

        if (PlayercountTDM < 2)
            return;
        TimeToStartTDM -= TimeSpan.FromSeconds(1);
    }
    
    private void TTTCHECK()
    {
        if (!CanJoinTTT)
        {
            TTTInfoUpdated?.Invoke(TimeSpan.FromSeconds(-1), PlayercountTTT);
            return;
        }
        TTTInfoUpdated?.Invoke(TimeToStartTTT, PlayercountTTT);

        if (PlayercountTTT < 4)
            return;

        TimeToStartTTT -= TimeSpan.FromSeconds(1);
    }

    /// <summary>
    /// Отправляем запрос на участие в тдме
    /// </summary>
    public void TPMeToTDM()
    {
        var msg = new TPMeToTDMEvent();
        RaiseNetworkEvent(msg);
    }
    /// <summary>
    /// Отправляем запрос на участие в TTT
    /// </summary>
    public void TPMeToTTT()
    {
        var msg = new TPMeToTTTEvent();
        RaiseNetworkEvent(msg);
    }
}
