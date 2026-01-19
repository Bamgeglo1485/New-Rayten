using Content.Shared.Damage.Systems;
using Content.Shared.Mobs.Systems;
using Content.Shared.Mobs;
using System.Linq;
namespace Content.Shared.Vanilla.Archon.BlindPredator;

public abstract class SharedBlindPredatorSystem : EntitySystem
{
    [Dependency] protected readonly MobStateSystem MobStateSys = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BlindPredatorComponent, ComponentStartup>(OnComponentStartup);
        SubscribeLocalEvent<BlindPredatorComponent, ComponentRemove>(OnComponentRemove);
        SubscribeLocalEvent<BlindPredatorComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<PredatorVisibleMarkComponent, ComponentStartup>(OnVictimStartup);
        SubscribeLocalEvent<PredatorVisibleMarkComponent, BeforeDamageChangedEvent>(OnBeforeDamageChanged);
        SubscribeLocalEvent<PredatorVisibleMarkComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnComponentStartup(EntityUid uid, BlindPredatorComponent component, ref ComponentStartup args)
    {
        var query = EntityQueryEnumerator<PredatorVisibleMarkComponent>();
        while (query.MoveNext(out var ent, out var mark))
            SetVisibility(ent, uid, false, mark);
    }

    private void OnComponentRemove(EntityUid uid, BlindPredatorComponent component, ComponentRemove args)
    {
        var query = EntityQueryEnumerator<PredatorVisibleMarkComponent>();
        while (query.MoveNext(out var ent, out var mark))
        {
            mark.Predators.Remove(uid);
            Dirty(ent, mark);
        }
    }

    private void OnDamageChanged(EntityUid uid, BlindPredatorComponent component, DamageChangedEvent args)
    {
        if (args.Origin == null)
            return;
        if (!TryComp<PredatorVisibleMarkComponent>(args.Origin.Value, out var mark))
            return;

        SetVisibility(args.Origin.Value, uid, true, mark);
    }

    private void OnMobStateChanged(EntityUid uid, PredatorVisibleMarkComponent component, MobStateChangedEvent ev)
    {
        if (ev.NewMobState == MobState.Alive)
            return;

        foreach (var predator in component.Predators.Keys.ToArray())
            SetVisibility(uid, predator, false, component);
    }

    private void OnBeforeDamageChanged(EntityUid uid, PredatorVisibleMarkComponent component, ref BeforeDamageChangedEvent args)
    {
        if (args.Origin == null)
            return;

        if (args.Origin == uid)
            return;

        if (!TryComp<BlindPredatorComponent>(args.Origin, out var predComp))
            return;

        if (predComp.CanSeeOthers)
            return;

        if (component.Predators.TryGetValue(args.Origin.Value, out var val) && !val)
            args.Cancelled = true;
    }

    private void OnVictimStartup(EntityUid uid, PredatorVisibleMarkComponent mark, ref ComponentStartup args)
    {
        var query = EntityQueryEnumerator<BlindPredatorComponent>();
        while (query.MoveNext(out var ent, out _))
            SetVisibility(uid, ent, false, mark);
    }

    public virtual void SetVisibility(EntityUid victim, EntityUid predator, bool visible, PredatorVisibleMarkComponent comp)
    {
        if (victim == predator)
            return;

        if (!Exists(victim) || Deleted(victim))
            return;

        if (MobStateSys.IsIncapacitated(victim))
            visible = false;

        if (comp.Predators.TryGetValue(predator, out var val) && val == visible)
            return;

        comp.Predators[predator] = visible;
        Dirty(victim, comp);
    }
}
