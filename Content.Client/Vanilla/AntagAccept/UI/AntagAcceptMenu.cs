using System.Numerics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using static Robust.Client.UserInterface.Controls.BoxContainer;
using Robust.Client.Graphics;

namespace Content.Client.Ghost.UI;

public sealed class AntagAcceptMenu : DefaultWindow
{
    public readonly Button DenyButton;
    public readonly Button AcceptButton;
    private readonly Label _roleLabel;
    private readonly Label _textLabel;

    public AntagAcceptMenu()
    {
        Title = Loc.GetString("antag-accept-title");

        _roleLabel = new Label
        {
            Text = "",
            FontColorOverride = new Color(1f, 0.8f, 0f),
            HorizontalAlignment = HAlignment.Center,
            Margin = new Thickness(0, 0, 0, 10)
        };

        _textLabel = new Label
        {
            Text = Loc.GetString("antag-accept-text"),
            HorizontalAlignment = HAlignment.Left,
            Margin = new Thickness(0, 0, 0, 15)
        };

        ContentsContainer.AddChild(new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            Children =
            {
                new BoxContainer
                {
                    Orientation = LayoutOrientation.Vertical,
                    Margin = new Thickness(10, 10, 10, 10),
                    Children =
                    {
                        _roleLabel,
                        _textLabel,
                        new BoxContainer
                        {
                            Orientation = LayoutOrientation.Horizontal,
                            Align = AlignMode.Center,
                            Margin = new Thickness(0, 10, 0, 0),
                            Children =
                            {
                                (AcceptButton = new Button
                                {
                                    Text = Loc.GetString("antag-accept-yes-text"),
                                    MinSize = new Vector2(120, 30)
                                }),

                                new Control
                                {
                                    MinSize = new Vector2(20, 0)
                                },

                                (DenyButton = new Button
                                {
                                    Text = Loc.GetString("antag-accept-no-text"),
                                    MinSize = new Vector2(120, 30)
                                })
                            }
                        },
                    }
                },
            }
        });
    }

    public void UpdateRoleName(string roleName)
    {
        _roleLabel.Text = $"Роль: {roleName}";
        _textLabel.Text = Loc.GetString("antag-accept-text-with-role", ("role", roleName));
    }
}
