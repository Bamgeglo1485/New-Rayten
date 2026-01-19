using Content.Shared.Vanilla.Archon.BlindPredator;
using Content.Shared.Movement.Components;
using Content.Client.Vanilla.Overlays.BlindPredator;
using Robust.Shared.GameStates;
using Robust.Shared.Player;
using Robust.Client.Graphics;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Microsoft.VisualBasic;

namespace Content.Client.Vanilla.Archon.BlindPredator;

public sealed class BlindPredatorSystem : SharedBlindPredatorSystem
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IOverlayManager _overlayMan = default!;
    private BlindPredatorOverlay _overlay = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PredatorVisibleMarkComponent, AfterAutoHandleStateEvent>(OnHandleState);
        SubscribeLocalEvent<PredatorVisibleMarkComponent, ComponentShutdown>(OnVictimShutdown);
        SubscribeLocalEvent<BlindPredatorComponent, LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<BlindPredatorComponent, LocalPlayerDetachedEvent>(OnPlayerDetached);
        _overlay = new(18f);
    }

    private void OnPlayerAttached(EntityUid uid, BlindPredatorComponent component, LocalPlayerAttachedEvent args)
    {
        _overlayMan.AddOverlay(_overlay);
        var query = EntityQueryEnumerator<PredatorVisibleMarkComponent>();
        while (query.MoveNext(out var ent, out var mark))
            UpdateVisibility(ent, mark);
    }

    private void OnPlayerDetached(EntityUid uid, BlindPredatorComponent component, LocalPlayerDetachedEvent args)
    {
        _overlayMan.RemoveOverlay(_overlay);
        var query = EntityQueryEnumerator<PredatorVisibleMarkComponent, SpriteComponent>();
        while (query.MoveNext(out _, out _, out var sprite))
            sprite.Visible = true;
    }

    public override void SetVisibility(EntityUid victim, EntityUid predator, bool visible, PredatorVisibleMarkComponent? comp = null)
    {
        if (!Resolve(victim, ref comp))
            return;

        if (MobStateSys.IsIncapacitated(victim))
            visible = false;

        if (comp.Predators.TryGetValue(predator, out var val) && val == visible)
            return;

        comp.Predators[predator] = visible;

        UpdateVisibility(victim, comp);
    }

    public void UpdateVisibility(EntityUid uid, PredatorVisibleMarkComponent? comp = null)
    {
        if (!Resolve(uid, ref comp))
            return;

        var locEnt = _playerManager.LocalSession?.AttachedEntity;
        if (locEnt == null)
            return;

        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        if (locEnt == uid)
        {
            sprite.Visible = true;
            return;
        }

        if (TryComp<BlindPredatorComponent>(locEnt, out var predComp) && predComp.CanSeeOthers)
        {
            sprite.Visible = true;
            return;
        }

        if (comp.Predators.TryGetValue(locEnt.Value, out var val))
            sprite.Visible = val;
        else
            sprite.Visible = true;
    }

    private void OnVictimShutdown(EntityUid uid, PredatorVisibleMarkComponent comp, ref ComponentShutdown args)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        sprite.Visible = true;
    }

    private void OnHandleState(EntityUid uid, PredatorVisibleMarkComponent comp, ref AfterAutoHandleStateEvent args)
    {
        UpdateVisibility(uid, comp);
    }
}
