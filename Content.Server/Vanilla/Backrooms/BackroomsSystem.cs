using Content.Server.Station.Systems;
using Content.Shared.Maps;
using Content.Shared.AlternateDimension;
using Content.Shared.Tag;
using Content.Shared.Magic.Components;
using Content.Shared.Atmos.Components;
using Content.Shared.Sprite;
using Content.Shared.Humanoid;
using Content.Shared.Overlays;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Fluids.Components;
using Content.Shared.Body;

using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

using System.Numerics;

namespace Content.Server.Backrooms;

public sealed partial class BackroomsSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedAlternateDimensionSystem _alternate = default!;
    [Dependency] private TagSystem _tagSystem = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedScaleVisualsSystem ScaleVisuals = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transformSystem = default!;

    public static readonly ProtoId<TagPrototype> _tableTag = new("Table");
    public static readonly ProtoId<TagPrototype> _pipeTag = new("Pipe");

    private static readonly Vector2[] _directions = new Vector2[]
{
        new(1, 0),
        new(-1, 0),
        new(0, 1),
        new(0, -1),
        new(1, 1),
        new(-1, 1),
        new(1, -1),
        new(-1, -1)
};

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BackroomsComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<HumanoidProfileComponent, EntParentChangedMessage>(OnParentChanged);
    }

    private void OnParentChanged(Entity<HumanoidProfileComponent> ent, ref EntParentChangedMessage args)
    {
        if (args.OldParent != null && HasComp<BackroomsComponent>(args.OldParent.Value) && HasComp<BackroomsOverlayComponent>(ent))
            RemComp<BackroomsOverlayComponent>(ent);
        else if (args.Transform.GridUid != null && HasComp<BackroomsComponent>(args.Transform.GridUid))
            EnsureComp<BackroomsOverlayComponent>(ent);
    }

    public void OnMapInit(Entity<BackroomsComponent> ent, ref MapInitEvent args)
    {
        if (!TryComp<AlternateDimensionGridComponent>(ent, out var dimension))
            return;

        ent.Comp.RealGrid = dimension.RealDimensionGrid;
        ent.Comp.DimensionType = dimension.DimensionType;

        Copy(ent);

        ent.Comp.NextCleaning = _timing.CurTime + ent.Comp.CleaningDelay;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQuery<BackroomsComponent>();
        foreach (var comp in query)
        {
            var ent = new Entity<BackroomsComponent>(comp.Owner, comp);

            HumanCopyProcess(ent);
            CleaningProcess(ent);
        }
    }

    private void HumanCopyProcess(Entity<BackroomsComponent> ent)
    {
        if (_timing.CurTime < ent.Comp.NextHumanCopy)
            return;

        ent.Comp.NextHumanCopy = _timing.CurTime + ent.Comp.HumanCopyDelay;

        CopyHuman(ent);
    }

    private void CleaningProcess(Entity<BackroomsComponent> ent)
    {
        if (_timing.CurTime < ent.Comp.NextCleaning)
            return;

        ent.Comp.NextCleaning = _timing.CurTime + ent.Comp.CleaningDelay;

        var puddleQuery = AllEntityQuery<PuddleComponent, TransformComponent>();
        while (puddleQuery.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.GridUid != ent)
                continue;

            QueueDel(uid);
        }

        var ammoQuery = AllEntityQuery<CartridgeAmmoComponent, TransformComponent>();
        while (ammoQuery.MoveNext(out var uid, out var ammo, out var xform))
        {
            if (xform.GridUid != ent || ammo.Spent == false)
                continue;

            QueueDel(uid);
        }

        var organQuery = AllEntityQuery<OrganComponent, TransformComponent>();
        while (organQuery.MoveNext(out var uid, out var organ, out var xform))
        {
            if (xform.GridUid != ent || organ.Body != null)
                continue;

            QueueDel(uid);
        }
    }

    public void Copy(Entity<BackroomsComponent> ent)
    {
        if (ent.Comp.DimensionType == null)
            return;

        var realGrid = ent.Comp.RealGrid;
        if (realGrid == null)
            return;

        var entitiesToCopy = new List<(string PrototypeId, EntityCoordinates Coordinates, Angle Rotation, EntityUid Source)>();

        var query = AllEntityQuery<AnimateableComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.GridUid != realGrid)
                continue;

            if (HasComp<AtmosDeviceComponent>(uid))
                continue;

            if (_tagSystem.HasTag(uid, _pipeTag) || _tagSystem.HasTag(uid, _tableTag))
                continue;

            if (!TryComp(uid, out MetaDataComponent? metaData))
                continue;

            if (_container.IsEntityOrParentInContainer(uid, metaData, xform))
                continue;

            if (!_random.Prob(ent.Comp.CopyChance))
                continue;

            var prototypeId = metaData.EntityPrototype?.ID;
            if (string.IsNullOrEmpty(prototypeId))
                continue;

            var coords = _alternate.GetAlternateRealityCoordinates(uid, ent.Comp.DimensionType);
            if (coords == null)
                continue;

            entitiesToCopy.Add((prototypeId, coords.Value, xform.LocalRotation, uid));
        }

        foreach (var (prototypeId, coords, rotation, source) in entitiesToCopy)
        {
            var spawned = Spawn(prototypeId, coords);
            if (TryComp(spawned, out TransformComponent? spawnedXform))
            {
                spawnedXform.LocalRotation = rotation;
            }

            var scale = new Vector2(_random.NextFloat(0.7f, 1.5f), _random.NextFloat(0.5f, 2.0f));
            ScaleVisuals.SetSpriteScale(spawned, scale);

            if (_random.Prob(ent.Comp.LineCopyChance))
            {
                var lineLength = _random.Next(ent.Comp.MinLine, ent.Comp.MaxLine + 1);
                var direction = _random.Pick(_directions);
                var spacing = ent.Comp.LineSpacing;

                var sourceCoords = _alternate.GetAlternateRealityCoordinates(source, ent.Comp.DimensionType);
                if (sourceCoords != null)
                {
                    for (int i = 1; i <= lineLength; i++)
                    {
                        var offset = direction * (spacing * i);
                        var linePosition = new Vector2(
                            sourceCoords.Value.X + offset.X,
                            sourceCoords.Value.Y + offset.Y
                        );

                        var lineCoords = new EntityCoordinates(sourceCoords.Value.EntityId, linePosition);

                        var lineSpawn = Spawn(prototypeId, lineCoords);
                        if (TryComp(lineSpawn, out TransformComponent? lineXform))
                        {
                            lineXform.LocalRotation = rotation + new Angle(_random.NextFloat(-0.5f, 0.5f));
                        }

                        var lineScale = new Vector2(
                            _random.NextFloat(0.5f, 1.8f),
                            _random.NextFloat(0.4f, 2.2f));
                        ScaleVisuals.SetSpriteScale(lineSpawn, lineScale);

                        if (i % 5 == 0)
                            _transformSystem.SetCoordinates(lineSpawn, lineCoords);
                    }
                }
            }
        }
    }

    public void CopyHuman(Entity<BackroomsComponent> ent)
    {
        if (ent.Comp.DimensionType == null)
            return;

        var realGrid = ent.Comp.RealGrid;
        if (realGrid == null)
            return;

        if (string.IsNullOrEmpty(ent.Comp.ClonePrototype))
            return;
        
        var humans = new List<EntityUid>();
        var query = AllEntityQuery<HumanoidProfileComponent, TransformComponent, MetaDataComponent>();

        while (query.MoveNext(out var uid, out _, out var xform, out var meta))
        {
            if (Deleted(uid))
                continue;

            if (xform.GridUid != realGrid)
                continue;

            humans.Add(uid);
        }

        if (humans.Count == 0)
            return;

        var randomHuman = _random.Pick(humans);

        if (Deleted(randomHuman))
            return;

        var altCoords = _alternate.GetAlternateRealityCoordinates(randomHuman, ent.Comp.DimensionType);
        if (altCoords == null)
            return;

        if (!altCoords.Value.IsValid(EntityManager))
            return;

       Spawn(ent.Comp.ClonePrototype, altCoords.Value);
    }
}
