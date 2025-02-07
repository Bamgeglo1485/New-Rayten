using Content.Shared.Popups;
using Content.Server.Popups;
using Content.Server.AlertLevel;
using Content.Shared.Vanilla.AlertKey;
using Content.Server.Station.Systems;
using Robust.Server.GameObjects;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Emag.Components;
using System.Linq;

namespace Content.Server.Vanilla.AlertKey;

public sealed class AlertKeySystem : EntitySystem
{
    [Dependency] private readonly AlertLevelSystem _alertLevelSystem = default!;
    [Dependency] private readonly AccessReaderSystem _accessReaderSystem = default!;
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

        // Если основной код не равен текущему и его нет в списке доступа, или если есть хотя бы один недоступный дополнительный код
        if ((message.Level != _alertLevelSystem.GetLevel(stationUid.Value) && !comp.CodeAccess.Contains(message.Level)) || 
            !message.Subcodestoadd.All(comp.CodeAccess.Contains) || 
            !message.Subcodestorem.All(comp.CodeAccess.Contains))
        {
            _popupSystem.PopupCursor(Loc.GetString("alert-key-no-access-pool"), message.Actor, PopupType.Medium);
            return;
        }

        // Проверяем, есть ли у станции компонент AlertLevelComponent
        if (!TryComp<AlertLevelComponent>(stationUid.Value, out var alertComp))
            return;

        // Устанавливаем основной уровень
        _alertLevelSystem.SetLevel(stationUid.Value, message.Level, true, true);

        // Удаляем подуровни из списка на удаление
        foreach (var subLevel in message.Subcodestorem)
        {
            _alertLevelSystem.RemSubLevel(stationUid.Value, subLevel, null, alertComp);
        }

        // Добавляем подуровни из списка на добавление
        foreach (var subLevel in message.Subcodestoadd)
        {
            _alertLevelSystem.SetSubLevel(stationUid.Value, subLevel, true, true);
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



