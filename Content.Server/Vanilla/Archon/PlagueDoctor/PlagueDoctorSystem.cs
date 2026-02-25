using Content.Server.Atmos.EntitySystems;
using Content.Server.Zombies;
using Content.Server.Chat.Systems;
using Content.Shared.Administration.Systems;
using Content.Shared.Atmos.Components;
using Content.Shared.Random;
using Content.Shared.Random.Helpers;
using Content.Shared.Vanilla.Archon.PlagueDoctor;
using Content.Shared.Vanilla.Archon.BlindPredator;
using Content.Shared.Gibbing;
using Content.Shared.Popups;
using Content.Shared.Cluwne;
using Content.Shared.Stunnable;
using Robust.Shared.Random;
using Robust.Shared.Prototypes;

namespace Content.Server.Vanilla.Archon.PlagueDoctor;

public sealed class PlagueDoctorgSystem : SharedPlagueDoctorgSystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly GibbingSystem _gibbing = default!;
    [Dependency] private readonly ZombieSystem _zombie = default!;
    [Dependency] private readonly RejuvenateSystem _rejuvenate = default!;
    [Dependency] private readonly FlammableSystem _flammableSystem = default!;
    [Dependency] private readonly ChatSystem _chatSystem = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;


    protected override void MakeSurgery(EntityUid uid, PlagueDoctorComponent comp, EntityUid target)
    {
        var surgeryResult = _proto.Index<WeightedRandomPrototype>(comp.SurgeryResults).Pick(_random);
        switch (surgeryResult)
        {
            case "Zombify":
                _zombie.ZombifyEntity(target);
                Popup.PopupEntity(Loc.GetString("archon049-surgery-success"), target, PopupType.Large);
                break;
            case "Gib":
                _gibbing.Gib(target, user: uid);
                Popup.PopupEntity(Loc.GetString("archon049-surgery-fail"), target, PopupType.Large);
                break;
            case "Rejuvenate":
                _rejuvenate.PerformRejuvenate(target);
                Popup.PopupEntity(Loc.GetString("archon049-surgery-fail"), target, PopupType.Large);
                break;
            case "Ignite":
                if (TryComp<FlammableComponent>(target, out var flammable))
                {
                    flammable.FireStacks = flammable.MaximumFireStacks;
                    _flammableSystem.Ignite(target, uid);
                }
                Popup.PopupEntity(Loc.GetString("archon049-surgery-fail"), target, PopupType.Large);
                break;
            case "Cluwnefy":
                EnsureComp<CluwneComponent>(target);
                Popup.PopupEntity(Loc.GetString("archon049-surgery-fail"), target, PopupType.Large);
                break;
        }
    }

    protected override void MakeRaging(EntityUid uid, PlagueDoctorComponent comp)
    {
        base.MakeRaging(uid, comp);
        var query = EntityQueryEnumerator<CrawlerComponent>();
        while (query.MoveNext(out var target, out var crawl))
        {
            if (uid == target)
                continue;
            _stun.TryKnockdown((target, crawl), TimeSpan.FromSeconds(6));
        }

        var victimQuery = EntityQueryEnumerator<PredatorVisibleMarkComponent>();
        while (victimQuery.MoveNext(out var victim, out var mark))
            BlindPredator.SetVisibility(victim, uid, true, mark);
    }

    protected override void MakeRage(EntityUid uid, PlagueDoctorComponent comp)
    {
        base.MakeRage(uid, comp);
        _chatSystem.DispatchGlobalAnnouncement(
            Loc.GetString("archon049-global-announcement"),
            Name(uid),
            playSound: true,
            comp.RageAnnounceSound,
            Color.Violet
        );
    }
}
