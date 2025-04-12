using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Content.Shared.Vanilla.Background;
using Content.Server.Mind;
using Content.Shared.Mind;
using Content.Server.Roles;
using Content.Server.Ghost.Roles;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Roles;
using Content.Shared.Vanilla.Jammer;
using Robust.Shared.GameObjects;


using Robust.Shared.Utility;

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

        _role.MindTryRemoveRole<MindRoleComponent>(mindId);
        _role.MindTryRemoveRole<GhostRoleMarkerRoleComponent>(mindId);
        _role.MindTryRemoveRole<NukeopsRoleComponent>(mindId);


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
