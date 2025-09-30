using Content.Server.NPC.Systems;
using Content.Server.NPC.HTN;
using Content.Server.NPC;
using Content.Shared.Vanilla.Entities.SecuritronWhistle;
using Content.Shared.Vanilla.Dominator;
using Robust.Shared.Map;
using System.Numerics;

namespace Content.Server.Vanilla.Entities.SecuritronWhistle;

public sealed class SecuritronWhistleSystem : SharedSecuritronWhistleSystem
{
    [Dependency] private readonly NPCSystem _npc = default!;
    [Dependency] private readonly HTNSystem _htn = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var query = EntityQueryEnumerator<SecurityMarkerComponent, HTNComponent>();
        var currentTime = _timing.CurTime;

        while (query.MoveNext(out var uid, out var marker, out var htn))
        {
            if (marker.UnFollowOn.GetValueOrDefault() < currentTime)
                StopFollowing(uid, htn, marker);
        }
    }

    protected override void FollowMe(EntityUid target, EntityUid master, SecurityMarkerComponent comp)
    {
        if (!TryComp<HTNComponent>(target, out var htn))
            return;

        if (comp.UnFollowOn != null)
            return;

        comp.UnFollowOn = _timing.CurTime + TimeSpan.FromSeconds(comp.FollowTime);

        if (htn.Plan != null)
            _htn.ShutdownPlan(htn);

        _npc.SetBlackboard(target, NPCBlackboard.FollowTarget, new EntityCoordinates(master, Vector2.Zero));

        _htn.Replan(htn);
    }

    private void StopFollowing(EntityUid target, HTNComponent htn, SecurityMarkerComponent marker)
    {
        if (htn.Plan != null)
            _htn.ShutdownPlan(htn);

        marker.UnFollowOn = null;

        // Убираем цель из blackboard
        htn.Blackboard.Remove<EntityCoordinates>(NPCBlackboard.FollowTarget);

        _htn.Replan(htn);
    }

}
