using Content.Shared.Vanilla.Background;
using Content.Shared.UserInterface;
using Content.Shared.Popups;
using Robust.Shared.Network;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared.Vanilla.Jammer;

public class SharedJammerSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<OverrideJammerTimeEvent>(Onoverride);
        SubscribeLocalEvent<SetJammerOnSpawnComponent, MapInitEvent>(SetJammerOnSpawn);
        SubscribeLocalEvent<RequiresNoJammerComponent, ActivatableUIOpenAttemptEvent>(OnActivate);//Открытие интерфейса
    }

    private void OnActivate(EntityUid uid, RequiresNoJammerComponent component, ref ActivatableUIOpenAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        var (isjammeractive, timetobreak) = CheckJammer();

        if (!isjammeractive)
            return;

        _popup.PopupCursor("Блюспейс-система заблокирована. Попробуйте позже.");
        args.Cancel();
    }


    private void SetJammerOnSpawn(EntityUid uid, SetJammerOnSpawnComponent component, MapInitEvent args)
    {
        SetJammer(component.Duration);
    }

    public void SetJammer(TimeSpan duration, EntityUid? station = null)
    {
        var newendtime = _timing.CurTime + duration;
        var query = EntityQueryEnumerator<StationJammerComponent>();
        while (query.MoveNext(out var stationUid, out var jammer))
        {
            if (station != null && station != stationUid)
                continue;

            if (jammer.JammerEndTime == null || newendtime > jammer.JammerEndTime)
                jammer.JammerEndTime = newendtime;

            Dirty(stationUid, jammer);
        }
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
        var curtime = _timing.CurTime;
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

    private void Onoverride(OverrideJammerTimeEvent ev)
    {
        SetJammer(TimeSpan.FromMinutes(ev.Minutes));
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
