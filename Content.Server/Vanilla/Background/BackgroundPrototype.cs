
using Content.Server.Mind;
using Content.Server.Roles;
using Content.Server.Ghost.Roles;
using Content.Server.Actions;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Mind;
using Content.Shared.Roles;
using Content.Shared.Actions;
using Content.Shared.Vanilla.Jammer;
using Content.Shared.Vanilla.Background;
using Content.Shared.Implants;
using Content.Shared.Inventory;
using Content.Shared.Clothing;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Set;
using Robust.Shared.Utility;
using JetBrains.Annotations;

namespace Content.Server.Vanilla.Background;

public sealed partial class ChangeMindSpecial : BackgroundSpecial
{
    [DataField(required: true)]
    public List<EntProtoId> MindRoles;

    public override void apply(EntityUid mob)
    {
        var entMan = IoCManager.Resolve<IEntityManager>();
        var _mind = entMan.System<MindSystem>();
        var _role = entMan.System<RoleSystem>();

        if (!_mind.TryGetMind(mob, out var mindId, out var mindcomp))
            return;

        // _role.MindRemoveRole<MindRoleComponent>(mindId);
        _role.MindRemoveRole<GhostRoleMarkerRoleComponent>(mindId);
        _role.MindRemoveRole<NukeopsRoleComponent>(mindId);


        _role.MindAddRoles(mindId, MindRoles, mindcomp);
    }
}

public sealed partial class AddItemSpecial : BackgroundSpecial
{
    [DataField(required: true)]
    public List<EntProtoId> Items;

    public override void apply(EntityUid mob)
    {
        var entMan = IoCManager.Resolve<IEntityManager>();
        var _hands = entMan.System<SharedHandsSystem>();
        foreach( var someitem in Items)
        {
            var transform = entMan.GetComponent<TransformComponent>(mob);
            var coordinates = transform.Coordinates;

            var item = entMan.SpawnEntity(someitem, coordinates);

            _hands.PickupOrDrop(mob, item);
        }

    }
}
public sealed partial class AddComponentsSpecial : BackgroundSpecial
{
    [DataField(required: true)]
    public ComponentRegistry Components { get; private set; }

    public override void apply(EntityUid mob)
    {
        var entMan = IoCManager.Resolve<IEntityManager>();
        entMan.AddComponents(mob, Components, removeExisting: true);
    }
}
public sealed partial class AddActionSpecial : BackgroundSpecial
{
    [DataField(required: true)]
    public EntProtoId Action { get; private set; }

    public override void apply(EntityUid mob)
    {
        var entMan = IoCManager.Resolve<IEntityManager>();
        var _mind = entMan.System<MindSystem>();
        var _actions = entMan.System<ActionsSystem>();
        var _actionContainer = entMan.System<ActionContainerSystem>();
        //give action
        if (!string.IsNullOrWhiteSpace(Action))
        {
            if (!_mind.TryGetMind(mob, out var mind, out _))
                _actions.AddAction(mob, Action);
            else
                _actionContainer.AddAction(mind, Action);
        }

    }
}
public sealed partial class AddImplantSpecial : BackgroundSpecial
{
    [DataField("implants", customTypeSerializer: typeof(PrototypeIdHashSetSerializer<EntityPrototype>))]
    public HashSet<String> Implants { get; private set; } = new();
    public override void apply(EntityUid mob)
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
    public override void apply(EntityUid mob)
    {
        var entityManager = IoCManager.Resolve<IEntityManager>();

        foreach( var specialevent in Events)
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
    public override void apply(EntityUid mob)
    {
        var entMan = IoCManager.Resolve<IEntityManager>();
        var _loadout = entMan.System<LoadoutSystem>();
        var _inventory = entMan.System<InventorySystem>();

        if (!entMan.TryGetComponent<InventoryComponent>(mob, out var inventory))
            return;

        foreach (var slotId in RemoveSlotID)
        {
            Logger.Info($"обрабатываем {slotId}");
            // Пытаемся снять предмет
            if (!_inventory.TryUnequip(mob, slotId, out var removedUid, silent: true))
                continue;

            if (entMan.EntityExists(removedUid))
                entMan.DeleteEntity(removedUid.Value);
        }
        _loadout.Equip(mob, Loadout, null);
    }
}
