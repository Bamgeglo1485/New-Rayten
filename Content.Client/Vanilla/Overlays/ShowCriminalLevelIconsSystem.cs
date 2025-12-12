using Content.Shared.StatusIcon;
using Content.Shared.StatusIcon.Components;
using Content.Shared.Vanilla.Overlays;
using Content.Shared.Overlays;
using Content.Client.Overlays;
using Content.Shared.Vanilla.Dominator;
using Robust.Shared.Prototypes;

namespace Content.Client.Vanilla.Overlays;

public sealed class ShowCriminalLevelIconsSystem : EquipmentHudSystem<ShowCriminalLevelIconsComponent>
{

    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly SharedDangerMobSystem _dangermob = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StatusIconComponent, GetStatusIconsEvent>(OnGetStatusIconsEvent);
    }
    private void OnGetStatusIconsEvent(EntityUid uid, StatusIconComponent _, ref GetStatusIconsEvent ev)
    {
        if (!IsActive)
            return;

        var DangerLevel = _dangermob.GetEntityDanger(uid);
        if (DangerLevel <= 0)
            return;

        var iconId = $"CriminalLevelIcon{DangerLevel}";

        if (_prototypeManager.TryIndex<CriminalLevelIconPrototype>(iconId, out var icon))
            ev.StatusIcons.Add(icon);
        else
            Logger.Error($"[CriminalLevelIcon] Invalid danger icon ID: {iconId}");
    }
}
