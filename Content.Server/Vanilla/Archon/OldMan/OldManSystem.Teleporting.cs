using Content.Shared.Vanilla.Archon.OldMan;
using Content.Shared.Administration;
using Content.Shared.Random;
using Content.Shared.Random.Helpers;
using Robust.Shared.Random;
namespace Content.Server.Vanilla.Archon.OldMan;

public sealed partial class OldManSystem : SharedOldManSystem
{
    private void ProcessTeleport(EntityUid uid, PDAnimationComponent animcomp, TimeSpan now)
    {
        if (now < animcomp.TeleportationEndAt)
            return;
        if (TryComp<OldManComponent>(uid, out var comp))
        {
            if (!animcomp.IsOut)
            {
                //вошли в телепорт
                var xform = Transform(uid);
                if (xform.GridUid is { } previousGrid)
                {
                    comp.FallBackCoords = xform.Coordinates;
                    comp.PreviousGrid = previousGrid;
                    _polymorph.PolymorphEntity(uid, "OldManJaunt");
                }
            }
        }
        else if (TryComp<DimensionVictimComponent>(uid, out var victimComp))
        {
            var rand = SharedRandomExtensions.PredictedRandom(timing, GetNetEntity(uid));
            if (!TryGetRandomExistingTile(victimComp.DimensionGridUid, out var coords))
                coords = Transform(victimComp.DimensionGridUid).Coordinates;

            trans.SetCoordinates(uid, coords.Value);
            RaiseNetworkEvent(new FallAnimationEvent(GetNetEntity(uid)));
        }
        RemComp<AdminFrozenComponent>(uid);
        RemComp<PDAnimationComponent>(uid);
    }
    protected override void TryStartAnimation(EntityUid uid, PDAnimationComponent comp)
    {
    }
    protected override void TryStopAnimation(EntityUid uid)
    {
    }
}
