using Content.Server.NPC.Systems;
using Content.Server.NPC.HTN;
using Content.Server.NPC;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Vanilla.Entities.SecuritronWhistle;
using Content.Shared.Vanilla.Dominator;
using Content.Shared.Pointing;
using Content.Shared.Cuffs.Components;
using Robust.Shared.Map;
using Robust.Shared.Containers;
using System.Numerics;

namespace Content.Server.Vanilla.Entities.SecuritronWhistle;

public sealed class SecuritronWhistleSystem : SharedSecuritronWhistleSystem
{
    [Dependency] private readonly NPCSystem _npc = default!;
    [Dependency] private readonly HTNSystem _htn = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SecuritronMasterComponent, ComponentShutdown>(OnMasterShutdown);
    }

    private void OnMasterShutdown(EntityUid uid, SecuritronMasterComponent master, ref ComponentShutdown args)
    {
        foreach (var bot in master.Securitrons)
        {
            if (!TryComp<HTNComponent>(bot, out var htn))
                continue;

            if (htn.Plan != null)
                _htn.ShutdownPlan(htn);

            if (htn.Blackboard.ContainsKey(NPCBlackboard.FollowTarget))
                htn.Blackboard.Remove<EntityCoordinates>(NPCBlackboard.FollowTarget);

            _htn.Replan(htn);

            //наручники убери
            if (TryComp<SecuritronComponent>(bot, out var security) && _hands.GetActiveItem(bot) is { } held)
            {
                if (HasComp<HandcuffComponent>(held))
                {
                    if (!_container.TryRemoveFromContainer(held))
                        return;

                    _container.Insert(held, security.HandCuffContainer);
                }
                else
                {
                    _hands.TryDrop(bot, held);
                }
            }

        }
    }

    protected override void FollowMe(EntityUid target, EntityUid master, SecuritronComponent comp, SecuritronMasterComponent mastercomp)
    {
        if (!TryComp<HTNComponent>(target, out var htn))
            return;

        mastercomp.Securitrons.Add(target);

        if (htn.Plan != null)
            _htn.ShutdownPlan(htn);

        _npc.SetBlackboard(target, NPCBlackboard.FollowTarget, new EntityCoordinates(master, Vector2.Zero));

        _htn.Replan(htn);
    }

}
