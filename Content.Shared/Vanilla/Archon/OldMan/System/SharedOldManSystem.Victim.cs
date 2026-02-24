using Content.Shared.Movement.Systems;
using Content.Shared.Overlays;
using Content.Shared.Jittering;
using Robust.Shared.Physics.Events;

namespace Content.Shared.Vanilla.Archon.OldMan;

public abstract partial class SharedOldManSystem : EntitySystem
{
    private void OnVictimShutDown(EntityUid uid, DimensionVictimComponent comp, ComponentShutdown args)
    {
        MovementSpeed.RefreshMovementSpeedModifiers(uid);
        RemComp<NoirOverlayComponent>(uid);
        RemComp<JitteringComponent>(uid);
        comp.Stream = Audio.Stop(comp.Stream);
        foreach (var portal in comp.Portals)
        {
            if (Exists(portal) && !Deleted(portal))
                PredictedQueueDel(portal);
        }
    }
    private void OnCollide(EntityUid uid, DimensionEscapeTeleportComponent comp, ref StartCollideEvent args)
    {
        if (!TryComp<DimensionVictimComponent>(args.OtherEntity, out var victim))
            return;

        if (!victim.ReturnableVictim)
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
