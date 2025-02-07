using Content.Shared.CCVar;
using Content.Shared.Chat;
using Content.Shared.Vanilla.AlertKey;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;

namespace Content.Client.Vanilla.AlertKey
{
    public sealed class AlertKeyBoundUserInterface : BoundUserInterface
    {
        private AlertKeyMenu? _menu;

        public AlertKeyBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
        {
        }

        protected override void Open()
        {
            base.Open();

            _menu = this.CreateWindow<AlertKeyMenu>();

            _menu.OnApply += ApplyAlertLevels;
        }
        private void ApplyAlertLevels(string mainCode, HashSet<string> subcodestoadd, HashSet<string> subcodestorem)
        {
            SendMessage(new AlertKeyApplyMessage(mainCode, subcodestoadd, subcodestorem));
        }

        protected override void UpdateState(BoundUserInterfaceState state)
        {
            base.UpdateState(state);

            if (state is not AlertKeyInterfaceState alertState)
                return;

            if (_menu != null)
            {
                _menu.Alerts = alertState.AlertLevels;
                _menu.AlertLevelSelectable = alertState.AlertLevels != null &&
                                               !float.IsNaN(alertState.CurrentAlertDelay) &&
                                               alertState.CurrentAlertDelay <= 0;

                _menu.UpdateAlertLevels(alertState.AlertLevels, alertState.CurrentAlert, alertState.ActiveSubLevels);
                _menu.ApplyButton.Disabled = !_menu.AlertLevelSelectable;
                _menu.MainSelection.Disabled = !_menu.AlertLevelSelectable;
            }
        }
    }
}
