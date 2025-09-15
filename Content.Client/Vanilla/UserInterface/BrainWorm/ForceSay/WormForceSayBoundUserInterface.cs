
using System.Linq;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Content.Shared.Vanilla.Entities.BrainWorm;

namespace Content.Client.Vanilla.UserInterface.BrainWorm.ForceSay;

public sealed class WormForceSayBoundUserInterface : BoundUserInterface
{

    [ViewVariables]
    private ForceSayWindow? _window;

    public WormForceSayBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }
    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<ForceSayWindow>();

        _window.AcceptButton.OnPressed += _ =>
        {
            var text = _window.Input.Text.Trim();
            if (!string.IsNullOrEmpty(text))
                Say(text);
            _window.Dispose();
            _window = null;
        };
    }
    public void Say(string text)
    {
        SendMessage(new ForceSayMessage(text));
    }
}
