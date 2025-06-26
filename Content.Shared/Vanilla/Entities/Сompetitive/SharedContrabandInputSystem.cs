using Content.Shared.Containers.ItemSlots;

namespace Content.Shared.Vanilla.Competitive;

public class SharedContrabandInputSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ContrabandInputComponent, ItemSlotEjectAttemptEvent>(OnEjectAttempt);
        SubscribeLocalEvent<ContrabandInputComponent, ItemSlotInsertAttemptEvent>(OnInsertAttempt);
    }

    private void OnInsertAttempt(Entity<ContrabandInputComponent> ent, ref ItemSlotInsertAttemptEvent args)
    {
        if (ent.Comp.Analysing)
            args.Cancelled = true;
    }

    private void OnEjectAttempt(Entity<ContrabandInputComponent> ent, ref ItemSlotEjectAttemptEvent args)
    {
        args.Cancelled = true;
    }

}
