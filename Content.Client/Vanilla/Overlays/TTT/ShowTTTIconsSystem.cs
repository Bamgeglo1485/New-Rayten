using Content.Shared.Overlays;
using Content.Shared.NukeOps;
using Content.Shared.StatusIcon;
using Content.Shared.StatusIcon.Components;
using Content.Client.Overlays;
using Content.Shared.Vanilla.Games.TTT;
using Robust.Shared.Prototypes;

namespace Content.Client.Vanilla.Overlays;

public sealed class ShowTTTIconsSystem : EquipmentHudSystem<ShowTTTDetectiveIconsComponent>
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TTTMarkerComponent, GetStatusIconsEvent>(OnGetStatusIconsEvent);
    }

    private void OnGetStatusIconsEvent(EntityUid uid, TTTMarkerComponent component, ref GetStatusIconsEvent ev)
    {
        if (!IsActive)
            return;

        if (component.Role == TTTRole.detective)
        {
            if (_prototype.TryIndex<FactionIconPrototype>(component.DecStatusIcon, out var deciconPrototype))
                ev.StatusIcons.Add(deciconPrototype);
        }
    }
}

public sealed class ShowTTTTraitorIconsSystem : EquipmentHudSystem<ShowTTTTraitorsComponent>
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

        if (_prototype.TryIndex<FactionIconPrototype>(component.SyndStatusIcon, out var syndiconPrototype))
            ev.StatusIcons.Add(syndiconPrototype);
    }
}

