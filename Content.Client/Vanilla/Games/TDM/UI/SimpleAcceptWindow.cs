using System.Numerics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Localization;
using static Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Client.Vanilla.TDM.UI
{
    public sealed class SimpleAcceptWindow : DefaultWindow
    {
        public readonly Button DenyButton;
        public readonly Button AcceptButton;
        public override void Close()
        {
            return;
        }
        public SimpleAcceptWindow(string title, string text, string acceptButton, string denyButton)
        {
            Title = title;

            Contents.AddChild(new BoxContainer
            {
                Orientation = LayoutOrientation.Vertical,
                Children =
                {
                    new BoxContainer
                    {
                        Orientation = LayoutOrientation.Vertical,
                        Children =
                        {
                            (new Label()
                            {
                                Text = text
                            }),
                            new BoxContainer
                            {
                                Orientation = LayoutOrientation.Horizontal,
                                Align = AlignMode.Center,
                                Children =
                                {
                                    (AcceptButton = new Button
                                    {
                                        Text = acceptButton,
                                    }),

                                    (new Control()
                                    {
                                        MinSize = new Vector2(20, 0)
                                    }),

                                    (DenyButton = new Button
                                    {
                                        Text = denyButton,
                                    })
                                }
                            },
                        }
                    },
                }
            });
        }
    }
}
