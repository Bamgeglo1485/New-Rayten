using Content.Shared.Movement.Systems;
using Content.Shared.Mobs;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Robust.Shared.Map.Components;
using Robust.Shared.Map;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics.Events;
using Robust.Shared.Timing;
using Robust.Shared.Random;
using Robust.Shared.Prototypes;
using Content.Shared.Movement.Events;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared.Damage.Systems;
using Content.Shared.CombatMode.Pacification;
using Content.Shared.ActionBlocker;
using Content.Shared.Actions;

namespace Content.Shared.Vanilla.Archon.OldMan;

public abstract partial class SharedOldManSystem : EntitySystem
{
    [Dependency] protected SharedActionsSystem Actions = default!;
    [Dependency] protected SharedAudioSystem Audio = default!;
    [Dependency] protected IGameTiming Timing = default!;
    [Dependency] protected SharedTransformSystem Trans = default!;
    [Dependency] protected MovementSpeedModifierSystem MovementSpeed = default!;
    [Dependency] protected SharedMapSystem MapSystem = default!;
    [Dependency] protected IPrototypeManager Proto = default!;
    [Dependency] private TurfSystem _turf = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private ActionBlockerSystem _blocker = default!;
    protected const float UpdateRate = 0.1f;
    protected float UpdateDif;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DimensionVictimComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMoveSpeed);
        SubscribeLocalEvent<DimensionVictimComponent, ComponentShutdown>(OnVictimShutDown);
        SubscribeLocalEvent<DimensionEscapeTeleportComponent, StartCollideEvent>(OnCollide);
        SubscribeLocalEvent<OldManComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<OldManComponent, UpdateCanMoveEvent>(OnUpdateCanMove);
        SubscribeLocalEvent<OldManComponent, DamageChangedEvent>(OnDamageChanged);

    }
    #region старик
    private void OnUpdateCanMove(EntityUid uid, OldManComponent comp, ref UpdateCanMoveEvent args)
    {
        if (comp.Eats)
            args.Cancel();
    }
    private void OnDamageChanged(EntityUid uid, OldManComponent comp, ref DamageChangedEvent args)
    {
        if (comp.Eats)
            comp.Eats = false;
        Dirty(uid, comp);
        RemComp<PacifiedComponent>(uid);
        _blocker.UpdateCanMove(uid);
        Actions.SetEnabled(comp.ActionEnt, true);
    }
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
    public virtual void RevertPolymorph(EntityUid oldMan)
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
        var tiles = MapSystem.GetAllTiles(gridUid, grid).ToList();
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
