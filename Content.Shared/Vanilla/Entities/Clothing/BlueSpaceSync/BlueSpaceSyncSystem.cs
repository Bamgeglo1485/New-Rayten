using Content.Shared.Vanilla.Entities.BlueSpaceSync;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Actions;
using Content.Shared.Stealth.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Stealth;
using Content.Shared.Damage.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;
using Content.Shared.Movement.Systems;

namespace Content.Shared.Vanilla.Entities.BlueSpaceSync;

public sealed class BlueSpaceSyncSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private readonly SharedStealthSystem _stealth = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BlueSpaceSyncAbilityComponent, BlueSpaceSyncEvent>(OnBlueSpaceSync);
        SubscribeLocalEvent<BlueSpaceSyncAbilityComponent, MapInitEvent>(OnInit);
        SubscribeLocalEvent<BlueSpaceSyncAbilityComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<BlueSpaceSyncComponent, MapInitEvent>(OnSyncInit);
        SubscribeLocalEvent<BlueSpaceSyncComponent, ComponentRemove>(OnSyncShutdown);
        SubscribeLocalEvent<BlueSpaceSyncComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMoveSpeed);
    }

    private void OnBlueSpaceSync(Entity<BlueSpaceSyncAbilityComponent> entity, ref BlueSpaceSyncEvent args)
    {
        var uid = args.Performer;

        var syncComp = EnsureComp<BlueSpaceSyncComponent>(uid);
        syncComp.EscapeTime = _timing.CurTime + entity.Comp.Duration;
        _audio.PlayPredicted(entity.Comp.EnterSound, uid, uid);
        args.Handled = true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var query = EntityQueryEnumerator<BlueSpaceSyncComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (_timing.CurTime >= comp.EscapeTime)
            {
                RemCompDeferred<BlueSpaceSyncComponent>(uid);
            }
        }
    }

    private void OnSyncInit(EntityUid uid, BlueSpaceSyncComponent component, ref MapInitEvent args)
    {
        _movementSpeed.RefreshMovementSpeedModifiers(uid);
        EnsureComp<RequireProjectileTargetComponent>(uid);
        _stealth.SetVisibility(uid, 0.45f, EnsureComp<StealthComponent>(uid));
    }

    private void OnSyncShutdown(EntityUid uid, BlueSpaceSyncComponent component, ref ComponentRemove args)
    {
        RemComp<StealthComponent>(uid);
        RemComp<RequireProjectileTargetComponent>(uid);
        _movementSpeed.RefreshMovementSpeedModifiers(uid);
    }

    private void OnRefreshMoveSpeed(EntityUid uid, BlueSpaceSyncComponent component, RefreshMovementSpeedModifiersEvent args)
    {
        args.ModifySpeed(component.WalkModifier, component.SprintModifier);
    }

    private void OnInit(Entity<BlueSpaceSyncAbilityComponent> entity, ref MapInitEvent args)
    {
        if (!TryComp(entity, out ActionsComponent? comp))
            return;

        _actions.AddAction(entity, ref entity.Comp.ActionEntity, entity.Comp.Action, component: comp);
    }

    private void OnShutdown(Entity<BlueSpaceSyncAbilityComponent> entity, ref ComponentShutdown args)
    {
        _actions.RemoveAction(entity.Owner, entity.Comp.ActionEntity);
    }
}
