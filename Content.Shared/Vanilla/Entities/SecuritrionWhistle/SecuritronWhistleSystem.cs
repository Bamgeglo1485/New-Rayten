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

    private void ExclamateTarget(EntityUid target, SecuritronWhistleComponent component)
    {
        SpawnAttachedTo(component.Effect, target.ToCoordinates());
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

        MakeLoudWhistle(uid, owner, component);
        return true;
    }

    private void MakeLoudWhistle(EntityUid uid, EntityUid owner, SecuritronWhistleComponent component)
    {
        foreach (var iterator in
            _entityLookup.GetEntitiesInRange<SecurityMarkerComponent>(_transform.GetMapCoordinates(uid), component.Distance))
        {
            if (iterator.Owner == owner)
                continue;

            ExclamateTarget(iterator, component);
            FollowMe(iterator, owner, iterator.Comp);
        }
    }
    //выполнение только на сервере
    protected abstract void FollowMe(EntityUid target, EntityUid master, SecurityMarkerComponent comp);
}
