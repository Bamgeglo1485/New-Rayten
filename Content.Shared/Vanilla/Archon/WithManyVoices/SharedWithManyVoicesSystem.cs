using Content.Shared.Vanilla.Archon.BlindPredator;
using Content.Shared.Animals.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Bed.Sleep;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics.Components;
using Robust.Shared.Timing;

namespace Content.Shared.Vanilla.Archon.WithManyVoices;

public abstract class SharedWithManyVoicesSystem : EntitySystem
{
    [Dependency] protected readonly IGameTiming Timing = default!;
    [Dependency] private readonly SharedBlindPredatorSystem _predator = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WithManyVoicesComponent, WithManyVoicesExoEvent>(OnExoEvent);
    }
    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var query = EntityQueryEnumerator<WithManyVoicesComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.SeeResetAt == null)
                continue;

            if (Timing.CurTime < comp.SeeResetAt)
                continue;

            comp.SeeResetAt = null;

            var victimQuery = EntityQueryEnumerator<PredatorVisibleMarkComponent>();
            while (victimQuery.MoveNext(out var ent, out var mark))
                _predator.SetVisibility(ent, uid, false, mark);

            Replan(uid);
        }
    }

    private void OnExoEvent(EntityUid uid, WithManyVoicesComponent comp, WithManyVoicesExoEvent args)
    {
        if (args.Handled)
            return;

        if (HasComp<SleepingComponent>(uid))
            return;

        comp.SeeResetAt = Timing.CurTime + comp.SeeTime;

        var uidTrans = Transform(uid).Coordinates;
        var victimQuery = EntityQueryEnumerator<InputMoverComponent, PredatorVisibleMarkComponent, PhysicsComponent, TransformComponent>();
        while (victimQuery.MoveNext(out var targetUid, out var input, out var mark, out var physics, out var xform))
        {
            var visibleDistance = input.Sprinting ? comp.VisibleDistanceRun : comp.VisibleDistanceWalk;

            if (physics.LinearVelocity.Length() < 0.1f)
                visibleDistance = comp.VisibleDistanceStand;

            if (!uidTrans.TryDistance(EntityManager, xform.Coordinates, out var distance))
                continue;

            _predator.SetVisibility(targetUid, uid, distance <= visibleDistance, mark);
        }
        _audio.PlayPredicted(comp.ExoSound, uid, uid);
        Replan(uid);
        args.Handled = true;
    }

    protected abstract void Replan(EntityUid uid);
}
