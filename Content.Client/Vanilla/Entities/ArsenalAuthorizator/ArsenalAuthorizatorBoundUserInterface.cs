using Content.Shared.Vanilla.Entities.ArsenalAuthorizator;
using Robust.Client.UserInterface;

namespace Content.Client.Vanilla.Entities.ArsenalAuthorizator;

public sealed class ArsenalAuthorizatorBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private ArsenalAuthorizatorWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<ArsenalAuthorizatorWindow>();
        _window.SetOwner(Owner);
        _window.OpenCentered();
        _window.OnArsenalAuthorizatorButtonPressed += OnDoorOpen;
    }

    private void OnDoorOpen(string id)
    {
        SendPredictedMessage(new ArsenalAuthorizatorOpenMessage(id));
    }

    public override void Update()
    {
        _window?.UpdateState();
    }
}
