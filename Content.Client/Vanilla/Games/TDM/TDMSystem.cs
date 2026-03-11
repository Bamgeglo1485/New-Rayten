using Content.Shared.Vanilla.TDM;
using Content.Shared.Projectiles;
using Robust.Shared.Timing;
using Content.Shared.GameTicking;
using Content.Client.Vanilla.TDM.UI;
using Content.Shared.Damage.Systems;
using Robust.Shared.Physics.Events;
using Content.Shared.Damage;

namespace Content.Client.Vanilla.TDM;

public sealed class TDMSystem : EntitySystem
{
    private SimpleAcceptWindow? _tdmWindow;

    [Dependency] private readonly IGameTiming _gameTiming = default!;
    public event Action<TimeSpan, int>? TDMInfoUpdated;
    public event Action<TimeSpan, int>? TTTInfoUpdated;

    private int _playercountTDM = 0;
    private int _playercountTTT = 0;

    private TimeSpan _timeToStartTDM = TimeSpan.FromSeconds(-1);
    private TimeSpan _timeToStartTTT = TimeSpan.FromSeconds(-1);

    private bool _canJoinTDM = false;
    private bool _canJoinTTT = false;

    public TimeSpan NextUpdate;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<TDMInformation>(RefreshTDMInformation);
        SubscribeNetworkEvent<TTTInformation>(RefreshTTTInformation);
        SubscribeNetworkEvent<RoundEndMessageEvent>(OnRoundEndMessage);
        SubscribeLocalEvent<TDMMarkerComponent, DamageModifyEvent>(OnDamageModify);
        SubscribeLocalEvent<TDMMarkerComponent, PreventCollideEvent>(OnPrventCollide);
    }
    //НО-френдлифаер
    private void OnDamageModify(EntityUid uid, TDMMarkerComponent component, DamageModifyEvent args)
    {
        if (!TryComp<TDMMarkerComponent>(args.Origin, out var sourcecomp))
            return;

        if (component.Team != sourcecomp.Team)
            return;

        // Полностью обнуляем урон
        args.Damage = new DamageSpecifier();
    }
    private void OnPrventCollide(EntityUid uid, TDMMarkerComponent component, ref PreventCollideEvent args)
    {
        if (args.Cancelled)
            return;

        if (!TryComp<ProjectileComponent>(args.OtherEntity, out var projectileComp))
            return;

        if (!TryComp<TDMMarkerComponent>(projectileComp.Shooter, out var otherMarker))
            return;

        if (otherMarker.Team == component.Team)
            args.Cancelled = true;
    }
    private void OnRoundEndMessage(RoundEndMessageEvent ev)
    {
        if (_tdmWindow != null && _tdmWindow.IsOpen)
            return;

        _tdmWindow = new SimpleAcceptWindow(Loc.GetString("accept-TDM-window-title"),
                                            Loc.GetString("accept-TDM-window-prompt-text-part"),
                                            Loc.GetString("accept-TDM-window-accept-button"),
                                            Loc.GetString("accept-TDM-window-deny-button"));

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
        _playercountTDM = msg.PlayerCount;
        _timeToStartTDM = msg.TimeToStart;
        _canJoinTDM = msg.CanJoin;
    }
    private void RefreshTTTInformation(TTTInformation msg, EntitySessionEventArgs args)
    {
        _playercountTTT = msg.PlayerCount;
        _timeToStartTTT = msg.TimeToStart;
        _canJoinTTT = msg.CanJoin;
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
        if (!_canJoinTDM)
        {
            TDMInfoUpdated?.Invoke(TimeSpan.FromSeconds(-1), _playercountTDM);
            return;
        }
        TDMInfoUpdated?.Invoke(_timeToStartTDM, _playercountTDM);

        if (_playercountTDM < 2)
            return;
        _timeToStartTDM -= TimeSpan.FromSeconds(1);
    }

    private void TTTCHECK()
    {
        if (!_canJoinTTT)
        {
            TTTInfoUpdated?.Invoke(TimeSpan.FromSeconds(-1), _playercountTTT);
            return;
        }
        TTTInfoUpdated?.Invoke(_timeToStartTTT, _playercountTTT);

        if (_playercountTTT < 3)
            return;

        _timeToStartTTT -= TimeSpan.FromSeconds(1);
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
