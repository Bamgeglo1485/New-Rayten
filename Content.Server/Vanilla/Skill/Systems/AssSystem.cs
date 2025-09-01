using Content.Shared.Implants;
using Robust.Shared.Containers;
using System.Linq;

namespace Content.Shared.Vanilla.Skill;

public sealed class ServerAssSystem : EntitySystem
{

    [Dependency] private readonly SharedSubdermalImplantSystem _subdermalImplant = default!;
    [Dependency] private readonly SharedTransformSystem _transformSystem = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    public const string BaseStorageId = "storagebase";
    public override void Initialize()
    {
        SubscribeLocalEvent<AssComponent, ComponentStartup>(OnInit);
        SubscribeLocalEvent<AssComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnInit(Entity<AssComponent> ent, ref ComponentStartup args)
    {
        ent.Comp.ImplantUid = _subdermalImplant.AddImplant(ent, ent.Comp.AssImplant);
    }

    private void OnShutdown(EntityUid uid, AssComponent component, ComponentShutdown args)
    {
        if(component.ImplantUid == null)
            return;

        if (!_container.TryGetContainer(component.ImplantUid.Value, BaseStorageId, out var AssImplant))
            return;

        var containedEntites = AssImplant.ContainedEntities.ToArray();

        foreach (var entity in containedEntites)
        {
            _transformSystem.DropNextTo(entity, uid);
        }

        _subdermalImplant.ForceRemove(uid, component.ImplantUid.Value);
    }
}
