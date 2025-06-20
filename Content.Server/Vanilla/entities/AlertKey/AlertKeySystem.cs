
using Content.Server.Popups;
using Content.Server.AlertLevel;
using Content.Server.Station.Systems;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Popups;
using Content.Shared.Emag.Components;
using Content.Shared.Vanilla.AlertKey;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;
using System.Linq;

namespace Content.Server.Vanilla.AlertKey;

public sealed class AlertKeySystem : EntitySystem
{
    [Dependency] private readonly AlertLevelSystem _alertLevelSystem = default!;
    [Dependency] private readonly AccessReaderSystem _accessReaderSystem = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly PopupSystem _popupSystem = default!;
    [Dependency] private readonly StationSystem _stationSystem = default!;
    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;
    public override void Initialize()
    {
        // All events that refresh the BUI
        SubscribeLocalEvent<AlertLevelChangedEvent>(OnAlertLevelChanged);
        SubscribeLocalEvent<AlertKeyComponent, ComponentInit>((uid, comp, _) => UpdateAlertKeyInterface(uid, comp));
        SubscribeLocalEvent<AlertLevelDelayFinishedEvent>(_ => OnGenericBroadcastEvent());

        // Messages from the BUI
        SubscribeLocalEvent<AlertKeyComponent, AlertKeyApplyMessage>(OnSelectAlertLevelMessage);
    }


    private void OnSelectAlertLevelMessage(EntityUid uid, AlertKeyComponent comp, AlertKeyApplyMessage message)
    {
        if (message.Actor is not { Valid: true } mob)
            return;

        if (!CanUse(mob, uid))
        {
            _popupSystem.PopupCursor(Loc.GetString("comms-console-permission-denied"), message.Actor, PopupType.Medium);
            return;
        }

        var stationUid = _stationSystem.GetOwningStation(uid);
        if (stationUid == null)
            return;

        if (!comp.CodeAccess.Contains(message.Level))
        {
            _popupSystem.PopupCursor(Loc.GetString("alert-key-no-access-pool"), message.Actor, PopupType.Medium);
            return;
        }

        // Проверяем, есть ли у станции компонент AlertLevelComponent
        if (!TryComp<AlertLevelComponent>(stationUid.Value, out var alertComp))
            return;

        string reason = "Неизвестна";

        if (_prototypeManager.TryIndex<AlertLevelReasonPrototype>(message.Reason, out var reasonproto))
        {
            reason = reasonproto.Text;
        }

        if (_prototypeManager.TryIndex<AlertLevelPrototype>("stationAlerts", out var proto))
        {
            if (proto.Levels.TryGetValue(message.Level, out var detail))
            {
                if (detail.Subcode)
                {
                    // Это сабкод
                    if (alertComp.ActiveSubLevels.ContainsKey(message.Level))
                    {
                        _alertLevelSystem.RemSubLevel(stationUid.Value, message.Level, null, alertComp);
                    }
                    else
                    {
                        _alertLevelSystem.SetSubLevel(stationUid.Value, message.Level, true, true, reason: reason);
                    }
                }
                else
                {
                    // Это основной код
                    if (message.Level == alertComp.CurrentLevel)
                        return;

                    _alertLevelSystem.SetLevel(stationUid.Value, message.Level, true, true, reason: reason);
                }
            }
        }
    }

    private void OnAlertLevelChanged(AlertLevelChangedEvent args)
    {
        var query = EntityQueryEnumerator<AlertKeyComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            var entStation = _stationSystem.GetOwningStation(uid);
            if (args.Station == entStation)
                UpdateAlertKeyInterface(uid, comp);
        }
    }

    /// <summary>
    /// Updates the UI for a particular comms console.
    /// </summary>
        public void UpdateAlertKeyInterface(EntityUid uid, AlertKeyComponent comp)
        {
            var stationUid = _stationSystem.GetOwningStation(uid);
            List<(string Level, bool IsSubcode, bool blocked)>? levels = null;
            string currentLevel = default!;
            HashSet<string> ActiveSubLevels = new HashSet<string>();
            float currentDelay = 0;

            if (stationUid != null)
            {
                if (TryComp(stationUid.Value, out AlertLevelComponent? alertComp) &&
                    alertComp.AlertLevels != null)
                {
                    if (alertComp.IsSelectable)
                    {
                        levels = new();
                        foreach (var (id, detail) in alertComp.AlertLevels.Levels)
                        {
                            if (detail.Selectable)
                            {
                                bool blocked = !comp.CodeAccess.Contains(id);
                                levels.Add((id, detail.Subcode, blocked));
                            }
                        }
                    }

                    currentLevel = alertComp.CurrentLevel;
                    ActiveSubLevels = new HashSet<string>(alertComp.ActiveSubLevels.Keys);
                    currentDelay = _alertLevelSystem.GetAlertLevelDelay(stationUid.Value, alertComp);
                }
            }

            _uiSystem.SetUiState(uid, AlertKeyUiKey.Key, new AlertKeyInterfaceState(
                levels,
                currentLevel,
                ActiveSubLevels,
                currentDelay
            ));
        }

        /// <summary>
        /// Update the UI of every comms console.
        /// </summary>
        private void OnGenericBroadcastEvent()
        {
            var query = EntityQueryEnumerator<AlertKeyComponent>();
            while (query.MoveNext(out var uid, out var comp))
            {
                UpdateAlertKeyInterface(uid, comp);
            }
        }
        private bool CanUse(EntityUid user, EntityUid alertkey)
        {
            if (TryComp<AccessReaderComponent>(alertkey, out var accessReaderComponent) && !HasComp<EmaggedComponent>(alertkey))
            {
                return _accessReaderSystem.IsAllowed(user, alertkey, accessReaderComponent);
            }
            return true;
        }
}



