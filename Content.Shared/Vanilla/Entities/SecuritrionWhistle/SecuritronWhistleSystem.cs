using Content.Shared.Coordinates;
using Content.Shared.Humanoid;
using Content.Shared.Interaction.Events;
using Content.Shared.Vanilla.Dominator;
using JetBrains.Annotations;
using Robust.Shared.Timing;

namespace Content.Shared.Vanilla.Entities.SecuritronWhistle;

public abstract class SharedSecuritronWhistleSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _entityLookup = default!;
    [Dependency] protected readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SecuritronWhistleComponent, UseInHandEvent>(OnUseInHand);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var query = EntityQueryEnumerator<SecuritronMasterComponent>();
        var currentTime = _timing.CurTime;

        while (query.MoveNext(out var uid, out var master))
        {
            if (master.UnFollowOn.GetValueOrDefault() < currentTime)
            {
                RemComp<SecuritronMasterComponent>(uid);
            }
        }
    }

    public void OnUseInHand(EntityUid uid, SecuritronWhistleComponent component, UseInHandEvent args)
    {
        if (args.Handled || !_timing.IsFirstTimePredicted)
            return;

        args.Handled = TryMakeLoudWhistle(uid, args.User, component);
    }

    public bool TryMakeLoudWhistle(EntityUid uid, EntityUid owner, SecuritronWhistleComponent? component = null)
    {
        if (!Resolve(uid, ref component, false) || component.Distance <= 0)
            return false;

        if (HasComp<SecuritronMasterComponent>(owner))
            return false;

        var mastercomp = EnsureComp<SecuritronMasterComponent>(owner);
        mastercomp.UnFollowOn = _timing.CurTime + TimeSpan.FromSeconds(mastercomp.FollowTime);

        MakeLoudWhistle(uid, owner, component, mastercomp);
        return true;
    }

    private void MakeLoudWhistle(EntityUid uid, EntityUid owner, SecuritronWhistleComponent component, SecuritronMasterComponent mastercomp)
    {
        foreach (var iterator in
            _entityLookup.GetEntitiesInRange<SecuritronComponent>(_transform.GetMapCoordinates(uid), component.Distance))
        {
            if (iterator.Owner == owner)
                continue;

            SpawnAttachedTo(component.Effect, iterator.Owner.ToCoordinates());

            FollowMe(iterator, owner, iterator.Comp, mastercomp);
        }
    }
    //выполнение только на сервере
    protected abstract void FollowMe(EntityUid target, EntityUid master, SecuritronComponent comp, SecuritronMasterComponent mastercomp);
}
