using Content.Client.Stylesheets.Palette;
using Content.Client.UserInterface.Controls;
using Content.Shared.Vanilla.Teleportation.Components;
using Content.Shared.Vanilla.Teleportation;
using Robust.Client.UserInterface;
using Robust.Shared.Map;

namespace Content.Client.Teleportation.UI;

public sealed class PortalGunBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private static readonly Color SelectedOptionColor = Palettes.Green.Element.WithAlpha(128);
    private static readonly Color SelectedOptionHoverColor = Palettes.Green.HoveredElement.WithAlpha(128);

    private SimpleRadialMenu? _menu;

    protected override void Open()
    {
        base.Open();

        if (!EntMan.TryGetComponent<PortalGunComponent>(Owner, out var portal))
            return;

        _menu = this.CreateWindow<SimpleRadialMenu>();
        var models = CreateButtons(portal.Index, portal.MaxIndex, portal.SavedCoordinates);
        _menu.SetButtons(models);

        _menu.OpenOverMouseScreenPosition();
    }

    private IEnumerable<RadialMenuOptionBase> CreateButtons(int currentIndex, int maxIndex, List<MapCoordinates?> savedCoords)
    {
        var options = new List<RadialMenuOptionBase>();

        for (var i = 0; i < maxIndex; i++)
        {
            Color? optionCustomColor = null;
            Color? optionHoverCustomColor = null;

            if (i == currentIndex)
            {
                optionCustomColor = SelectedOptionColor;
                optionHoverCustomColor = SelectedOptionHoverColor;
            }

            var label = $"Ячейка {i + 1}";

            if (i < savedCoords.Count)
            {
                var indexedSavedCoords = savedCoords[i];
                if (indexedSavedCoords != null)
                {
                    var coords = indexedSavedCoords.Value;
                    label += $"\nX:{coords.Position.X:F0} Y:{coords.Position.Y:F0}";
                }
                else
                {
                    label += "\n(пусто)";
                }
            }
            else
            {
                label += "\n(пусто)";
            }

            var option = new RadialMenuActionOption<int>(HandleRadialMenuClick, i)
            {
                ToolTip = label,
                BackgroundColor = optionCustomColor,
                HoverBackgroundColor = optionHoverCustomColor
            };

            options.Add(option);
        }

        return options;
    }

    private void HandleRadialMenuClick(int index)
    {
        var msg = new PortalGunIndexChangeMessage { Index = index };
        SendPredictedMessage(msg);
    }
}
