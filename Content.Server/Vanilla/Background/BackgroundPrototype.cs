
using Content.Server.Mind;
using Content.Server.Roles;
using Content.Server.Actions;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Roles.Components;
using Content.Shared.Roles;
using Content.Shared.Actions;
using Content.Shared.Vanilla.Background;
using Content.Shared.Implants;
using Content.Shared.Inventory;
using Content.Shared.Clothing;
using Robust.Shared.Prototypes;

namespace Content.Server.Vanilla.Background;

public sealed partial class ChangeMindSpecial : BackgroundSpecial
{
    [DataField(required: true)]
    public List<EntProtoId> MindRoles;

    public override void Apply(EntityUid mob)
    {
        var entMan = IoCManager.Resolve<IEntityManager>();
        var mind = entMan.System<MindSystem>();
        var role = entMan.System<RoleSystem>();

        if (!mind.TryGetMind(mob, out var mindId, out var mindcomp))
            return;

        // _role.MindRemoveRole<MindRoleComponent>(mindId);
        role.MindRemoveRole<GhostRoleMarkerRoleComponent>(mindId);
        role.MindRemoveRole<NukeopsRoleComponent>(mindId);


        role.MindAddRoles(mindId, MindRoles, mindcomp);
    }
}

public sealed partial class AddItemSpecial : BackgroundSpecial
{
    [DataField(required: true)]
    public List<EntProtoId> Items;

    public override void Apply(EntityUid mob)
    {
        var entMan = IoCManager.Resolve<IEntityManager>();
        var hands = entMan.System<SharedHandsSystem>();
        foreach (var someitem in Items)
        {
            var transform = entMan.GetComponent<TransformComponent>(mob);
            var coordinates = transform.Coordinates;

            var item = entMan.SpawnEntity(someitem, coordinates);

            hands.PickupOrDrop(mob, item);
        }

    }
}
public sealed partial class AddComponentsSpecial : BackgroundSpecial
{
    [DataField(required: true)]
    public ComponentRegistry Components { get; private set; }

    public override void Apply(EntityUid mob)
    {
        var entMan = IoCManager.Resolve<IEntityManager>();
        entMan.AddComponents(mob, Components, removeExisting: true);
    }
}
public sealed partial class AddActionSpecial : BackgroundSpecial
{
    [DataField(required: true)]
    public EntProtoId Action { get; private set; }

    public override void Apply(EntityUid mob)
    {
        var entMan = IoCManager.Resolve<IEntityManager>();
        var mind = entMan.System<MindSystem>();
        var actions = entMan.System<ActionsSystem>();
        var actionContainer = entMan.System<ActionContainerSystem>();
        //give action
        if (!string.IsNullOrWhiteSpace(Action))
        {
            if (!mind.TryGetMind(mob, out var mindComp, out _))
                actions.AddAction(mob, Action);
            else
                actionContainer.AddAction(mindComp, Action);
        }

    }
}
public sealed partial class AddImplantSpecial : BackgroundSpecial
{
    [DataField("implants")]
    public HashSet<EntProtoId> Implants { get; private set; } = new();

    public override void Apply(EntityUid mob)
    {
        var entMan = IoCManager.Resolve<IEntityManager>();
        var implantSystem = entMan.System<SharedSubdermalImplantSystem>();
        implantSystem.AddImplants(mob, Implants);
    }
}

public sealed partial class RaiseEventSpecial : BackgroundSpecial
{
    [DataField(required: true)]
    public List<BackgroundEvent> Events { get; private set; }
    public override void Apply(EntityUid mob)
    {
        var entityManager = IoCManager.Resolve<IEntityManager>();

        foreach (var specialevent in Events)
        {
            entityManager.EventBus.RaiseEvent(EventSource.Local, (object)specialevent);
        }
    }
}
public sealed partial class EquipSpecial : BackgroundSpecial
{
    [DataField(required: true)]
    public List<string> RemoveSlotID { get; private set; }

    [DataField("loadout")]
    public List<ProtoId<StartingGearPrototype>> Loadout = new();
    public override void Apply(EntityUid mob)
    {
        var entMan = IoCManager.Resolve<IEntityManager>();
        var loadout = entMan.System<LoadoutSystem>();
        var inventory = entMan.System<InventorySystem>();
        foreach (var slotId in RemoveSlotID)
        {
            // Пытаемся снять предмет
            if (!inventory.TryUnequip(mob, slotId, out var removedUid, silent: true))
                continue;

            if (entMan.EntityExists(removedUid))
                entMan.DeleteEntity(removedUid.Value);
        }
        loadout.Equip(mob, Loadout, null);
    }
}
