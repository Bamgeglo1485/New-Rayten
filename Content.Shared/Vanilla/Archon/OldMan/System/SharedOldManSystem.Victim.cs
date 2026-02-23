using Content.Shared.Movement.Systems;
using Content.Shared.Overlays;
using Content.Shared.Jittering;
using Content.Shared.Fluids;
using Content.Shared.Chemistry.Components;
using Content.Shared.Damage.Systems;
using Robust.Shared.Physics.Events;

namespace Content.Shared.Vanilla.Archon.OldMan;

public abstract partial class SharedOldManSystem : EntitySystem
{
    [Dependency] private readonly SharedStaminaSystem _stamina = default!;
    [Dependency] private readonly SharedPuddleSystem _puddle = default!;
    private void OnVictimShutDown(EntityUid uid, DimensionVictimComponent comp, ComponentShutdown args)
    {
        movementSpeed.RefreshMovementSpeedModifiers(uid);
        RemComp<NoirOverlayComponent>(uid);
        RemComp<JitteringComponent>(uid);
        comp.Stream = audio.Stop(comp.Stream);
        foreach (var portal in comp.Portals)
        {
            if (Exists(portal) && !Deleted(portal))
                PredictedQueueDel(portal);
        }
        RaiseLocalEvent(new FallAnimationEvent(GetNetEntity(uid)));
        uid.SpawnTimer(TimeSpan.FromSeconds(1), () => AfterFall(uid));
    }
    private void AfterFall(EntityUid uid)
    {
        _stamina.TakeStaminaDamage(uid, 200f);
        // audio.PlayPvs(comp.BodyFallSound, uid);
        var solution = new Solution();
        solution.AddReagent("Corrosion", 20f);
        _puddle.TrySpillAt(uid, solution, out _);
    }
    private void OnCollide(EntityUid uid, DimensionEscapeTeleportComponent comp, ref StartCollideEvent args)
    {
        if (!TryComp<DimensionVictimComponent>(args.OtherEntity, out var victim))
            return;

        PredictedQueueDel(uid);

        if (comp.IsFake)
            return;

        ReturnVictimOnStation(args.OtherEntity, victim);
    }
    private void OnRefreshMoveSpeed(EntityUid uid, DimensionVictimComponent component, RefreshMovementSpeedModifiersEvent args)
    {
        args.ModifySpeed(0.5f, 0.5f);
    }
    public virtual void ReturnVictimOnStation(EntityUid uid, DimensionVictimComponent comp)
    {
    }

}
