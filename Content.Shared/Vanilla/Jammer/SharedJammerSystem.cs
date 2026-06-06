using Content.Shared.Vanilla.Background;
using Content.Shared.UserInterface;
using Content.Shared.Popups;
using Robust.Shared.Timing;

namespace Content.Shared.Vanilla.Jammer;

public abstract partial class SharedJammerSystem : EntitySystem
{
    [Dependency] protected IGameTiming Timing = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RequiresNoJammerComponent, ActivatableUIOpenAttemptEvent>(OnActivate);
    }

    private void OnActivate(EntityUid uid, RequiresNoJammerComponent component, ref ActivatableUIOpenAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        var (isjammeractive, _) = CheckJammer();

        if (!isjammeractive)
            return;

        _popup.PopupCursor("Блюспейс-система заблокирована. Попробуйте позже.");
        args.Cancel();
    }
    public void RemoveJammer(EntityUid? station = null)
    {
        var query = EntityQueryEnumerator<StationJammerComponent>();
        while (query.MoveNext(out var stationUid, out var jammer))
        {
            if (station != null && station != stationUid)
                continue;

            jammer.JammerEndTime = null;
            Dirty(stationUid, jammer);
        }
    }

    public (bool isActive, TimeSpan timetobreak) CheckJammer(EntityUid? station = null)
    {
        var curtime = Timing.CurTime;
        var query = EntityQueryEnumerator<StationJammerComponent>();
        while (query.MoveNext(out var stationUid, out var jammer))
        {
            if (station != null && station != stationUid)
                continue;

            if (jammer.JammerEndTime != null)
            {
                if (curtime > jammer.JammerEndTime)
                    RemoveJammer(stationUid);
                else
                    return (true, jammer.JammerEndTime.Value - curtime);
            }
        }
        return (false, TimeSpan.Zero);
    }
}

[DataDefinition]
public sealed partial class OverrideJammerTimeEvent : BackgroundEvent
{
    [DataField("minutes")]
    public float Minutes = 5;

    public OverrideJammerTimeEvent() { }

    public OverrideJammerTimeEvent(float minutes)
    {
        Minutes = minutes;
    }
}
