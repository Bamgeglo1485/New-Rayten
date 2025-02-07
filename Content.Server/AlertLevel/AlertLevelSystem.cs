
using Content.Server.Chat.Systems;
using Content.Server.Station.Systems;
using Content.Shared.Vanilla.CCVars;
using Content.Shared.CCVar;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using System.Linq;

namespace Content.Server.AlertLevel;

public sealed class AlertLevelSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly ChatSystem _chatSystem = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly StationSystem _stationSystem = default!;

    public const string DefaultAlertLevelSet = "stationAlerts";
    public override void Initialize()
    {
        SubscribeLocalEvent<StationInitializedEvent>(OnStationInitialize);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypeReload);
    }

    public override void Update(float time)
    {
        var query = EntityQueryEnumerator<AlertLevelComponent>();

        while (query.MoveNext(out var station, out var alert))
        {
            if (alert.CurrentDelay > 0)
            {
                alert.CurrentDelay -= time;
                if (alert.CurrentDelay <= 0 && alert.ActiveDelay)
                {
                    RaiseLocalEvent(new AlertLevelDelayFinishedEvent());
                    alert.ActiveDelay = false;
                }
            }

            if (alert.CurrentTimeToNewCode > 0)
            {
                alert.CurrentTimeToNewCode -= time;
                if (alert.CurrentTimeToNewCode <= 0)
                {
                    Downcode(station, alert);
                }
            }

            foreach (var subLevel in alert.ActiveSubLevels.Keys.ToArray()) // ToArray() для избежания модификации коллекции во время итерации
            {
                if ((alert.ActiveSubLevels[subLevel] -= time) <= 0)
                    RemSubLevel(station, subLevel, null, alert);
            }
        }
    }


    //метод уменьшает код угрозы на 1 уровень 
    private void Downcode(EntityUid station, AlertLevelComponent alert)
    {
        if (alert.AlertLevels == null)
            return;

        string currentLevel = alert.CurrentLevel;

        if (!alert.AlertLevels.Levels.TryGetValue(currentLevel, out var detail))
            return;

        if (!detail.Subcode && detail.Position is int position && position > 0)
        {
            var downLevel = alert.AlertLevels.Levels
                .Where(kvp => kvp.Value.Position == position - 1)
                .Select(kvp => kvp.Key)
                .FirstOrDefault();

            if (!string.IsNullOrEmpty(downLevel))
            {
                SetLevel(station, downLevel, true, true, true);
            }
        }
    }


    private void OnStationInitialize(StationInitializedEvent args)
    {
        if (!TryComp<AlertLevelComponent>(args.Station, out var alertLevelComponent))
            return;

        if (!_prototypeManager.TryIndex(alertLevelComponent.AlertLevelPrototype, out AlertLevelPrototype? alerts))
        {
            return;
        }

        alertLevelComponent.AlertLevels = alerts;

        var defaultLevel = alertLevelComponent.AlertLevels.DefaultLevel;
        if (string.IsNullOrEmpty(defaultLevel))
        {
            defaultLevel = alertLevelComponent.AlertLevels.Levels.Keys.First();
        }

        SetLevel(args.Station, defaultLevel, false, false, true);
    }

    private void OnPrototypeReload(PrototypesReloadedEventArgs args)
    {
        if (!args.ByType.TryGetValue(typeof(AlertLevelPrototype), out var alertPrototypes)
            || !alertPrototypes.Modified.TryGetValue(DefaultAlertLevelSet, out var alertObject)
            || alertObject is not AlertLevelPrototype alerts)
        {
            return;
        }

        var query = EntityQueryEnumerator<AlertLevelComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            comp.AlertLevels = alerts;

            if (!comp.AlertLevels.Levels.ContainsKey(comp.CurrentLevel))
            {
                var defaultLevel = comp.AlertLevels.DefaultLevel;
                if (string.IsNullOrEmpty(defaultLevel))
                {
                    defaultLevel = comp.AlertLevels.Levels.Keys.First();
                }

                SetLevel(uid, defaultLevel, true, true, true);
            }
        }

        RaiseLocalEvent(new AlertLevelPrototypeReloadedEvent());
    }

    public string GetLevel(EntityUid station, AlertLevelComponent? alert = null)
    {
        if (!Resolve(station, ref alert))
        {
            return string.Empty;
        }

        return alert.CurrentLevel;
    }

    public float GetAlertLevelDelay(EntityUid station, AlertLevelComponent? alert = null)
    {
        if (!Resolve(station, ref alert))
        {
            return float.NaN;
        }

        return alert.CurrentDelay;
    }

    public void SetLevel(EntityUid station, string level, bool playSound, bool announce, bool force = false,
        bool locked = false, MetaDataComponent? dataComponent = null, AlertLevelComponent? component = null)
    {
        if (!Resolve(station, ref component, ref dataComponent)
            || component.AlertLevels == null
            || !component.AlertLevels.Levels.TryGetValue(level, out var detail)
            || component.CurrentLevel == level
            || detail.Subcode) // Игнорируем дополнительные коды
        {
            return;
        }

        if (!force)
        {
            if (!detail.Selectable || component.CurrentDelay > 0 || component.IsLevelLocked)
            {
                return;
            }

            component.CurrentDelay = _cfg.GetCVar(CCVars.GameAlertLevelChangeDelay);
            component.ActiveDelay = true;
        }
        if (detail.Position is int position && position > 0)
            component.CurrentTimeToNewCode = _cfg.GetCVar(CCVarsVanilla.GameAlertLevelDownDelay);
        else
            component.CurrentTimeToNewCode = 0;
            
        component.CurrentLevel = level;
        component.IsLevelLocked = locked;

        var stationName = dataComponent.EntityName;
        var name = Loc.TryGetString($"alert-level-{level}", out var locName) ? locName.ToLower() : level.ToLower();
        var announcement = Loc.TryGetString(detail.Announcement, out var locAnnouncement) ? locAnnouncement : detail.Announcement;
        var announcementFull = Loc.GetString("alert-level-announcement", ("name", name), ("announcement", announcement));

        var playDefault = false;
        if (playSound)
        {
            if (detail.Sound != null)
            {
                var filter = _stationSystem.GetInOwningStation(station);
                _audio.PlayGlobal(detail.Sound, filter, true, detail.Sound.Params);
            }
            else
            {
                playDefault = true;
            }
        }

        if (announce)
        {
            _chatSystem.DispatchStationAnnouncement(station, announcementFull, playDefaultSound: playDefault,
                colorOverride: detail.Color, sender: stationName);
        }

        RaiseLocalEvent(new AlertLevelChangedEvent(station, level));
    }

    /// <summary>
    /// Устанавливает дополнительный код угрозы для станции.
    /// </summary>
    public void SetSubLevel(EntityUid station, string subLevel, bool playSound, bool announce, bool force = false,
        bool locked = false, MetaDataComponent? dataComponent = null, AlertLevelComponent? component = null)
    {
        if (!Resolve(station, ref component, ref dataComponent)
            || component.AlertLevels == null
            || !component.AlertLevels.Levels.TryGetValue(subLevel, out var detail)
            || component.ActiveSubLevels.ContainsKey(subLevel))
        {
            return;
        }

        if (!force)
        {
            if (!detail.Selectable)
            {
                return;
            }
            component.CurrentDelay = _cfg.GetCVar(CCVars.GameAlertLevelChangeDelay);
            component.ActiveDelay = true;
        }

        component.ActiveSubLevels[subLevel] = _cfg.GetCVar(CCVarsVanilla.GameAlertLevelDownDelay);

        var stationName = dataComponent.EntityName;
        var name = Loc.TryGetString($"alert-level-{subLevel}", out var locName) ? locName.ToLower() : subLevel.ToLower();
        var announcement = Loc.TryGetString(detail.Announcement, out var locAnnouncement) ? locAnnouncement : detail.Announcement;
        var announcementFull = Loc.GetString("alert-level-announcement", ("name", name), ("announcement", announcement));

        var playDefault = false;
        if (playSound)
        {
            if (detail.Sound != null)
            {
                var filter = _stationSystem.GetInOwningStation(station);
                _audio.PlayGlobal(detail.Sound, filter, true, detail.Sound.Params);
            }
            else
            {
                playDefault = true;
            }
        }

        if (announce)
        {
            _chatSystem.DispatchStationAnnouncement(station, announcementFull, playDefaultSound: playDefault,
                colorOverride: detail.Color, sender: stationName);
        }

        RaiseLocalEvent(new AlertLevelChangedEvent(station, subLevel));
    }

    public void RemSubLevel(EntityUid station, string subLevel, MetaDataComponent? dataComponent = null, AlertLevelComponent? component = null)
    {
        if (!Resolve(station, ref component, ref dataComponent) || component == null)
            return;

        if (!component.ActiveSubLevels.ContainsKey(subLevel) || component.AlertLevels == null)
            return;

        if (component.AlertLevels.Levels.TryGetValue(subLevel, out var detail))
        {
            var stationName = dataComponent?.EntityName ?? "Unknown Station";
            var name = Loc.TryGetString($"alert-level-{subLevel}", out var locName) ? locName.ToLower() : subLevel.ToLower();
            var announcement = Loc.TryGetString(detail.AnnouncementDisable, out var locAnnouncement) ? locAnnouncement : detail.Announcement;
            var announcementFull = Loc.GetString("alert-level-announcement-disable", ("name", name), ("announcement", announcement));

            _chatSystem.DispatchStationAnnouncement(station, announcementFull, playDefaultSound: true,
                colorOverride: detail.Color, sender: stationName);
        }

        component.ActiveSubLevels.Remove(subLevel);

        component.CurrentDelay = _cfg.GetCVar(CCVars.GameAlertLevelChangeDelay);
        component.ActiveDelay = true;

        RaiseLocalEvent(new AlertLevelChangedEvent(station, subLevel));
    }


}

public sealed class AlertLevelDelayFinishedEvent : EntityEventArgs
{}

public sealed class AlertLevelPrototypeReloadedEvent : EntityEventArgs
{}

public sealed class AlertLevelChangedEvent : EntityEventArgs
{
    public EntityUid Station { get; }
    public string AlertLevel { get; }

    public AlertLevelChangedEvent(EntityUid station, string alertLevel)
    {
        Station = station;
        AlertLevel = alertLevel;
    }
}
