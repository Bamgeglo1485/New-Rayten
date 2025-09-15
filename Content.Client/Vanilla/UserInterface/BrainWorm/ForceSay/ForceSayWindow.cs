using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using static Robust.Client.UserInterface.Controls.BoxContainer;
using Robust.Shared.Localization;

namespace Content.Client.Vanilla.UserInterface.BrainWorm.ForceSay;

public sealed class ForceSayWindow : DefaultWindow
{
    public readonly Button AcceptButton;
    public readonly LineEdit Input;

    public ForceSayWindow()
    {
        Title = Loc.GetString("force-say-window-title");

        Contents.AddChild(new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            MinWidth = 320,
            Children =
            {
                new Label
                {
                    Text = Loc.GetString("force-say-text")
                },
                (Input = new LineEdit
                {
                    PlaceHolder = Loc.GetString("force-say-placeholder"),
                    HorizontalExpand = true
                }),
                new BoxContainer
                {
                    Orientation = LayoutOrientation.Horizontal,
                    Align = BoxContainer.AlignMode.Center,
                    Children =
                    {
                        (AcceptButton = new Button
                        {
                            Text = Loc.GetString("force-say-send-button")
                        })
                    }
                }
            }
        });
    }
}
