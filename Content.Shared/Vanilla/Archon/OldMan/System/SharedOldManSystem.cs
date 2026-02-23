using Content.Shared.Audio;
using Content.Shared.Administration;
using Content.Shared.Atmos.Rotting;
using Content.Shared.Bed.Sleep;
using Content.Shared.Movement.Systems;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Popups;
using Content.Shared.Polymorph;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Maps;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Physics;
using Content.Shared.Humanoid;
using Content.Shared.FixedPoint;
using Content.Shared.Random;
using Content.Shared.Random.Helpers;
using Robust.Shared.Map.Components;
using Robust.Shared.Map;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics.Events;
using Robust.Shared.Timing;
using Robust.Shared.Random;
using Robust.Shared.Prototypes;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection.Metadata;
using System.Runtime.InteropServices.Marshalling;

namespace Content.Shared.Vanilla.Archon.OldMan;

public abstract partial class SharedOldManSystem : EntitySystem
{
    [Dependency] protected readonly SharedAppearanceSystem appearance = default!;
    [Dependency] protected readonly SharedAudioSystem audio = default!;
    [Dependency] protected readonly IGameTiming timing = default!;
    [Dependency] protected readonly SharedTransformSystem trans = default!;
    [Dependency] protected readonly MovementSpeedModifierSystem movementSpeed = default!;
    [Dependency] protected readonly SharedMapSystem mapSystem = default!;
    [Dependency] protected readonly IPrototypeManager proto = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    protected const float UpdateRate = 0.1f;
    protected float _updateDif;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DimensionVictimComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMoveSpeed);
        SubscribeLocalEvent<DimensionVictimComponent, ComponentShutdown>(OnVictimShutDown);
        SubscribeLocalEvent<DimensionEscapeTeleportComponent, StartCollideEvent>(OnCollide);
        SubscribeLocalEvent<OldManComponent, MobStateChangedEvent>(OnMobStateChanged);
    }
    #region старик
    /// <summary>
    /// при полиморфе запоминаем полиморф чтобы потом ревертнуть его
    /// </summary>
    private void OnMobStateChanged(EntityUid uid, OldManComponent comp, MobStateChangedEvent args)
    {
        if (args.OldMobState > args.NewMobState)
            return;
        //возвращаем всех на станцию
        ReturnAllVictims(uid);

        //отмена тп при смерти
        if (args.NewMobState == MobState.Dead)
            RemComp<PDAnimationComponent>(uid);
    }
    public virtual void ReturnAllVictims(EntityUid oldMan)
    {
    }
    protected virtual void RevertPolymorph(EntityUid oldMan)
    {
    }
    #endregion
    #region helpers
    //ищет рандомный тайл, на который можно что-то поставить
    public bool TryGetRandomExistingTile(EntityUid gridUid, [NotNullWhen(true)] out EntityCoordinates? coords)
    {
        coords = null;
        if (!Exists(gridUid) || Deleted(gridUid))
            return false;

        if (!TryComp<MapGridComponent>(gridUid, out var grid))
            return false;
        var tiles = mapSystem.GetAllTiles(gridUid, grid).ToList();
        _random.Shuffle(tiles);
        foreach (var tile in tiles)
        {
            if (_turf.IsTileBlocked(tile, CollisionGroup.MobMask))
                continue;

            coords = new EntityCoordinates(gridUid, tile.GridIndices);
            return true;
        }
        return false;
    }
    #endregion

}
