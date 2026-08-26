using System.Threading;
using System.Threading.Tasks;
using Content.Shared.Maps;
using Content.Shared.AlternateDimension;
using Content.Shared.Tag;
using Robust.Shared.CPUJob.JobQueues;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos.Components;
using System.Numerics;
using Content.Shared.Storage;
using Content.Server.GameTicking.Rules.VariationPass.Components.ReplacementMarkers;
using Robust.Shared.Random;

namespace Content.Server.AlternateDimension;

public sealed class SpawnAlternateDimensionJob : Job<bool>
{
    private readonly IEntityManager _entManager;
    private readonly IPrototypeManager _prototypeManager;
    private readonly SharedMapSystem _mapSystem;
    private readonly SharedMapSystem _mapManager;
    private readonly ITileDefinitionManager _tileDefManager;
    private readonly TileSystem _tileSystem;
    private readonly EntityLookupSystem _lookup;
    private readonly TagSystem _tag;
    private readonly IRobustRandom _random;

    private readonly MapId _alternateMapId;

    private readonly EntityUid _alternateGrid;
    private readonly EntityUid _originalGrid;
    private readonly AlternateDimensionParams _alternateParams;

    private readonly Queue<(string Prototype, EntityCoordinates Coordinates, Angle Rotation)> _queuedSpawns = new();

    public SpawnAlternateDimensionJob(
        double maxTime,
        IEntityManager entManager,
        SharedMapSystem mapManager,
        IPrototypeManager protoManager,
        SharedMapSystem map,
        ITileDefinitionManager tileDefManager,
        TileSystem tileSystem,
        EntityLookupSystem lookup,
        TagSystem tagSystem,
        IRobustRandom random,
        MapId alternateMapId,
        EntityUid alternateGrid,
        EntityUid originalGrid,
        AlternateDimensionParams alternateParams,
        CancellationToken cancellation = default) : base(maxTime, cancellation)
    {
        _entManager = entManager;
        _mapManager = mapManager;
        _prototypeManager = protoManager;
        _mapSystem = map;
        _tileDefManager = tileDefManager;
        _tileSystem = tileSystem;
        _lookup = lookup;
        _tag = tagSystem;
        _random = random;
        _alternateMapId = alternateMapId;
        _alternateGrid = alternateGrid;
        _originalGrid = originalGrid;
        _alternateParams = alternateParams;
    }

    protected override async Task<bool> Process()
    {
        if (!_entManager.TryGetComponent<MapGridComponent>(_originalGrid, out var stationGridComp))
            return false;
        if (!_entManager.TryGetComponent<MapGridComponent>(_alternateGrid, out var alternateGridComp))
            return false;

        var random = new Random(_alternateParams.Seed);
        var mirrorCoordinates = _alternateParams.Dimension.MirrorCoordinates;

        //Add map components
        if (_alternateParams.Dimension.MapComponents is not null)
            _entManager.AddComponents(_mapSystem.GetMap(_alternateMapId), _alternateParams.Dimension.MapComponents);

        var localAABB = stationGridComp.LocalAABB;
        var minY = (int)Math.Floor(localAABB.Bottom);
        var maxY = (int)Math.Ceiling(localAABB.Top);
        var gridHeight = maxY - minY;

        //silhouette tiles
        var stationTiles = _mapSystem.GetAllTilesEnumerator(_originalGrid, stationGridComp);
        var alternateTiles = new List<(Vector2i Index, Tile Tile)>();
        var tileDef = _tileDefManager[_alternateParams.Dimension.DefaultTile];

        while (stationTiles.MoveNext(out var tileRef))
        {
            var originalIndex = tileRef.Value.GridIndices;
            Vector2i mirroredIndex;

            if (mirrorCoordinates)
            {
                var mirroredY = minY + (gridHeight - 1) - (originalIndex.Y - minY);
                mirroredIndex = new Vector2i(originalIndex.X, mirroredY);
            }
            else
            {
                mirroredIndex = originalIndex;
            }

            var tileVariant = _tileSystem.PickVariant((ContentTileDefinition)tileDef, random.Next());
            alternateTiles.Add((mirroredIndex, new Tile(tileDef.TileId, variant: tileVariant)));
        }
        _mapSystem.SetTiles((_alternateGrid, alternateGridComp), alternateTiles);

        //Add grid components
        if (_alternateParams.Dimension.GridComponents is not null)
            _entManager.AddComponents(_alternateGrid, _alternateParams.Dimension.GridComponents);

        //Set alternate dimension entities
        HashSet<Entity<TagComponent, TransformComponent>> taggedEntities = new();
        _lookup.GetChildEntities(_originalGrid, taggedEntities);

        foreach (var tagged in taggedEntities)
        {
            var originalXform = tagged.Comp2;
            var position = originalXform.Coordinates.Position;
            var rotation = originalXform.LocalRotation;

            Vector2 mirroredPosition;
            Angle mirroredRotation;

            if (mirrorCoordinates)
            {
                var tileIndices = _mapSystem.CoordinatesToTile(_originalGrid, stationGridComp,
                    new EntityCoordinates(_originalGrid, position));

                var mirroredTileY = minY + (gridHeight - 1) - (tileIndices.Y - minY);

                var tileOffsetX = position.X - tileIndices.X;
                var tileOffsetY = position.Y - tileIndices.Y;

                var mirroredY = mirroredTileY + tileOffsetY;
                mirroredPosition = new Vector2(position.X, (float)mirroredY);
                mirroredRotation = new Angle(Math.PI - rotation.Theta);
            }
            else
            {
                mirroredPosition = position;
                mirroredRotation = rotation;
            }

            var coord = new EntityCoordinates(_mapSystem.GetMap(_alternateMapId), mirroredPosition);

            string? spawnedPrototypeId = null;

            if (_entManager.TryGetComponent(tagged.Owner, out MetaDataComponent? metaData) &&
                metaData.EntityPrototype != null &&
                _alternateParams.Dimension.PrototypeReplacements.TryGetValue(
                    new EntProtoId(metaData.EntityPrototype.ID), out var prototypeReplacement))
            {
                spawnedPrototypeId = prototypeReplacement;
            }
            else
            {
                foreach (var replacement in _alternateParams.Dimension.Replacements)
                {
                    if (_tag.HasTag(tagged.Owner, replacement.Key))
                    {
                        spawnedPrototypeId = replacement.Value;
                        break;
                    }
                }
            }

            if (!string.IsNullOrEmpty(spawnedPrototypeId))
            {
                var spawned = _entManager.SpawnEntity(spawnedPrototypeId, coord);

                if (_entManager.TryGetComponent<TransformComponent>(spawned, out var spawnedXform))
                {
                    spawnedXform.LocalRotation = mirroredRotation;
                }
            }
        }

        //Final
        _mapManager.InitializeMap(_alternateMapId);
        _mapManager.SetPaused(_alternateMapId, false);

        ProcessWallReplacements(_alternateParams.Dimension);

        while (_queuedSpawns.TryDequeue(out var tup))
        {
            var (prototype, coords, rot) = tup;
            var newEnt = _entManager.SpawnEntity(prototype, coords);

            if (_entManager.TryGetComponent<TransformComponent>(newEnt, out var newXform))
            {
                newXform.LocalRotation = rot;
            }
        }

        var atmosSystem = _entManager.System<AtmosphereSystem>();
        var mapGrid = _entManager.EnsureComponent<MapGridComponent>(_alternateGrid);
        var gridAtmos = _entManager.EnsureComponent<GridAtmosphereComponent>(_alternateGrid);
        atmosSystem.RebuildGridAtmosphere((_alternateGrid, gridAtmos, mapGrid));

        return true;
    }

    private void ProcessWallReplacements(AlternateDimensionConfig dimension)
    {
        if (dimension.PortalWallReplacements == null || dimension.PortalWallReplacements.Count == 0)
            return;

        var replacementMod = _random.NextGaussian(dimension.PortalWallReplacementAverage, dimension.PortalWallReplacementStdDev);
        var prob = (float)Math.Clamp(1 / replacementMod, 0f, 1f);

        if (prob <= 0)
            return;

        var originalMapID = _entManager.GetComponent<TransformComponent>(_originalGrid).MapID;

        var wallsToReplace = new List<EntityUid>();

        var query = _entManager.EntityQuery<WallReplacementMarkerComponent, TransformComponent>();
        foreach (var (marker, xform) in query)
        {
            var uid = xform.Owner;
            if (xform.MapID != originalMapID)
                continue;

            if (_random.Prob(prob))
            {
                wallsToReplace.Add(uid);
            }
        }

        if (wallsToReplace.Count == 0)
            return;

        foreach (var wallUid in wallsToReplace)
        {
            if (_entManager.Deleted(wallUid))
                continue;

            var xform = _entManager.GetComponent<TransformComponent>(wallUid);
            var coords = xform.Coordinates;
            var rot = xform.LocalRotation;

            _entManager.QueueDeleteEntity(wallUid);

            foreach (var spawn in EntitySpawnCollection.GetSpawns(dimension.PortalWallReplacements, _random))
            {
                _queuedSpawns.Enqueue((spawn, coords, rot));
            }
        }
    }
}
