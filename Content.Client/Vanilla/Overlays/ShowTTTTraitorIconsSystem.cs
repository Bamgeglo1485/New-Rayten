using Content.Shared.Overlays;
using Content.Shared.NukeOps;
using Content.Shared.StatusIcon;
using Content.Shared.StatusIcon.Components;
using Content.Client.Overlays;
using Content.Shared.Vanilla.TDM;
using Robust.Shared.Prototypes;

namespace Content.Client.Vanilla.Overlays;

public sealed class ShowTTTTraitorIconsSystem : EquipmentHudSystem<ShowTTTTraitorsIconsComponent>
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TTTTRAITORComponent, GetStatusIconsEvent>(OnGetStatusIconsEvent);
    }

    private void OnGetStatusIconsEvent(EntityUid uid, TTTTRAITORComponent component, ref GetStatusIconsEvent ev)
    {
        if (!IsActive)
            return;

        if (_prototype.TryIndex<FactionIconPrototype>(component.SyndStatusIcon, out var iconPrototype))
            ev.StatusIcons.Add(iconPrototype);
    }


}
public sealed class ShowTTTDetectiveIconsSystem : EquipmentHudSystem<ShowTTTDetectiveIconsComponent>
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TTTDetectiveComponent, GetStatusIconsEvent>(OnGetStatusIconsEvent);
    }

    private void OnGetStatusIconsEvent(EntityUid uid, TTTDetectiveComponent component, ref GetStatusIconsEvent ev)
    {
        if (!IsActive)
            return;

        if (_prototype.TryIndex<FactionIconPrototype>(component.DecStatusIcon, out var iconPrototype))
            ev.StatusIcons.Add(iconPrototype);
    }
}