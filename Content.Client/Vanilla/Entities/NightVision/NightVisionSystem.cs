using Content.Shared.Vanilla.Entities.NightVision;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Client.Vanilla.NightVision;
using Content.Client.Overlays;

using Robust.Shared.Player;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;

namespace Content.Client.Vanilla.Entities.NightVision;

public sealed class NightVisionSystem : EquipmentHudSystem<NightVisionOverlayComponent>
{
    [Dependency] private readonly PointLightSystem _pointLightSystem = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IOverlayManager _overlayMan = default!;

    private NightVisionOverlay _overlay = default!;
    private EntityUid? _localplayer = null;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<NightVisionComponent, LocalPlayerAttachedEvent>(OnAttached);
        SubscribeLocalEvent<NightVisionComponent, LocalPlayerDetachedEvent>(OnDetached);
        _overlay = new();
    }

    protected override void UpdateInternal(RefreshEquipmentHudEvent<NightVisionOverlayComponent> component)
    {
        base.UpdateInternal(component);
        _overlayMan.AddOverlay(_overlay);

        _localplayer = _player.LocalSession?.AttachedEntity;

        if (_localplayer == null)
            return;

        switchlight(_localplayer.Value, true);
    }

    protected override void DeactivateInternal()
    {
        base.DeactivateInternal();
        _overlayMan.RemoveOverlay(_overlay);

        if (_localplayer == null)
            return;

        switchlight(_localplayer.Value, false);
        _localplayer = null;
    }

    private void OnAttached(EntityUid uid, NightVisionComponent component, LocalPlayerAttachedEvent args)
    {
        switchlight(uid, true);
    }

    private void OnDetached(EntityUid uid, NightVisionComponent component, LocalPlayerDetachedEvent args)
    {
        switchlight(uid, false);
    }

    private void switchlight(EntityUid uid, bool enable)
    {
        if (TryComp<PointLightComponent>(uid, out var pointLight))
            _pointLightSystem.SetEnabled(uid, enable, pointLight);
    }

}
