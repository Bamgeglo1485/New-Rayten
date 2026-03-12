using Content.Shared.Vanilla.Games.TTT.Items.Radar;
using Robust.Client.UserInterface;
using Robust.Shared.Map;

namespace Content.Client.Vanilla.Games.TTT.Items.Radar;

public sealed class TTTRadarBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private TTTRadarWindow? _window;

    protected override void Open()
    {
        base.Open();
        EntityUid? gridUid = null;
        if (EntMan.TryGetComponent<TransformComponent>(Owner, out var xform))
            gridUid = xform.GridUid;

        _window = this.CreateWindow<TTTRadarWindow>();
        _window.Title = EntMan.GetComponent<MetaDataComponent>(Owner).EntityName;

        if (!EntMan.TryGetComponent<TTTRadarComponent>(Owner, out var radar))
            return;

        _window.Set(gridUid, radar.TrackedEntities, radar.NextScan, radar.NextScan - TimeSpan.FromMinutes(0.5));
    }
    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (_window == null)
            return;
        if (state is not TTTRadarInterfaceState msg)
            return;
        if (!EntMan.TryGetComponent<TTTRadarComponent>(Owner, out var radar))
            return;
        _window.Set(radar.TrackedEntities, radar.NextScan, radar.NextScan - TimeSpan.FromMinutes(0.5));
    }
}
