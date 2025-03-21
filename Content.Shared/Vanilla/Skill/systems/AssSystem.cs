using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Containers;
using System.Linq;
using Robust.Shared.Random;

namespace Content.Shared.Vanilla.Skill;

public sealed class SharedAssSystem : EntitySystem
{

    [Dependency] protected readonly SharedUserInterfaceSystem UI = default!;
    [Dependency] protected readonly SharedStorageSystem _sharedStorageSystem = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedTransformSystem _transformSystem = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    public const string BaseStorageId = "storagebase";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StorageComponent, OpenAssStorageEvent>(OnAssActivate);
        SubscribeLocalEvent<AssComponent, AttackedEvent>(OnAttack);
    }

    private void OnAttack(EntityUid uid, AssComponent asscomp, AttackedEvent args)
    {
        
        if(asscomp.ImplantUid == null)
            return;

        if (!_container.TryGetContainer(asscomp.ImplantUid.Value, BaseStorageId, out var AssImplant))
            return;

        var containedEntites = AssImplant.ContainedEntities.ToArray();

        foreach (var entity in containedEntites)
        {
            if(_random.Prob(asscomp.dropchance))
                _transformSystem.DropNextTo(entity, uid);
        }
    }

    private void OnAssActivate(EntityUid uid, StorageComponent storageComp, OpenAssStorageEvent args)
    {
        if (args.Handled)
            return;

        var uiOpen = UI.IsUiOpen(uid, StorageComponent.StorageUiKey.Key, args.Performer);

        if (uiOpen)
            UI.CloseUi(uid, StorageComponent.StorageUiKey.Key, args.Performer);
        else
            _sharedStorageSystem.OpenStorageUI(uid, args.Performer, storageComp, false);

        args.Handled = true;
    }

}