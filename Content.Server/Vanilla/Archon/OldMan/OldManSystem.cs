using Content.Server.NPC.Systems;
using Content.Server.NPC;

using Content.Shared.Vanilla.Archon.OldMan;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Damage.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Overlays;
using Content.Shared.Humanoid;
using Content.Shared.Examine;
using Content.Shared.Damage;
using Content.Shared.Popups;
using Content.Shared.Audio;

using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Map;

using Robust.Server.GameObjects;

using System.Numerics;
using System.Linq;

namespace Content.Server.Vanilla.Archon.OldMan;

public sealed class OldManSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _trans = default!;
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly NPCSystem _npc = default!;

    private TimeSpan _nextUpdate = TimeSpan.Zero;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<OldManComponent, ComponentShutdown>(OnComponentShutdown);
        SubscribeLocalEvent<OldManComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<OldManComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(EntityUid uid, OldManComponent comp, ref MapInitEvent args)
    {
        var waitTime = _random.NextFloat(comp.MinWaitTime, comp.MaxWaitTime);
        comp.NextChaseStart = _timing.CurTime + TimeSpan.FromSeconds(waitTime);

        _mapLoader.TryLoadMap(comp.DimensionMap, out var dimension, out _);

        if (dimension == null)
            return;

        if (!TryComp<MapComponent>(dimension, out var mapComp))
            return;

        _mapSystem.InitializeMap(mapComp.MapId);

        TeleportToDimension(uid, comp);

        comp.DimensionUid = dimension;

        if (comp.DimensionUid != null)
            EnsureComp<PocketDimensionComponent>(comp.DimensionUid.Value);

        Dirty(uid, comp);
    }

    private void OnComponentShutdown(EntityUid uid, OldManComponent comp, ref ComponentShutdown args)
    {
        if (comp.DimensionUid.HasValue && !Deleted(comp.DimensionUid.Value))
            QueueDel(comp.DimensionUid.Value);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;

        if (curTime < _nextUpdate)
            return;

        _nextUpdate = curTime + TimeSpan.FromSeconds(1);

        var query = EntityQueryEnumerator<OldManComponent>();

        while (query.MoveNext(out var uid, out var comp))
        {

            if ((!comp.InDimension && curTime >= comp.ChaseEnd)
                || (comp.Target == null && comp.InDimension == false)
                || comp.Target != null && !HasComp<TransformComponent>(comp.Target.Value))
            {
                ReturnToDimension(uid, comp);
                continue;
            }

            if (comp.Target != null && Transform(comp.Target.Value).GridUid == null)
            {
                ReturnToDimension(uid, comp);
                continue;
            }

            if (comp.Target != null)
            {
                var victimPos = _trans.GetMapCoordinates(comp.Target.Value).Position;
                var oldmanPos = _trans.GetMapCoordinates(uid).Position;

                var dir = oldmanPos - victimPos;
                var length = dir.Length();

                if (comp.Target != null && Transform(uid).GridUid != null && length > 23)
                {
                    ReturnToDimension(uid, comp, false);
                    comp.AnimationStartTime = curTime;

                    continue;
                }
            }

            if (comp.InDimension && comp.AnimationShown && curTime >= comp.AnimationStartTime + comp.SpawnAnimationDelay)
            {
                StartChase(uid, comp);
                continue;
            }

            if (comp.InDimension && curTime >= comp.NextChaseStart && !comp.AnimationShown)
            {
                PreChase(uid, comp);
            }
        }
    }

    private void PreChase(EntityUid uid, OldManComponent comp)
    {
        comp.Target = FindTarget(uid, comp);

        if (comp.Target == null)
        {
            comp.NextChaseStart = _timing.CurTime + TimeSpan.FromSeconds(comp.RepeatedWaitTime);
            Dirty(uid, comp);
            return;
        }

        var targetCoords = Transform(comp.Target.Value).Coordinates;
        comp.TeleportCoordinates = targetCoords;
        var anim = Spawn(comp.SpawnAnimation, targetCoords);
        _popup.PopupEntity("Оно начинает выходить из под пола...", anim, PopupType.LargeCaution);

        comp.AnimationShown = true;
        comp.AnimationStartTime = _timing.CurTime;

        Dirty(uid, comp);
    }

    private void StartChase(EntityUid uid, OldManComponent comp)
    {
        var target = comp.Target;

        if (target == null)
            return;

        _trans.SetCoordinates(uid, comp.TeleportCoordinates);

        comp.InDimension = false;
        comp.ChaseEnd = _timing.CurTime + comp.ChaseDelay;
        comp.AnimationShown = false;

        if (comp.SpawnSound != null)
            _audio.PlayPvs(comp.SpawnSound, uid);

        Dirty(uid, comp);
    }

    private EntityUid? FindTarget(EntityUid uid, OldManComponent comp)
    {
        EntityUid? target = null;
        var highestDamage = 0f;

        var damageableQuery = EntityQueryEnumerator<DamageableComponent, HumanoidAppearanceComponent>();

        while (damageableQuery.MoveNext(out var entity, out var damageable, out var hyina))
        {
            if (entity == uid)
                continue;

            if (!_mobState.IsAlive(entity))
                continue;

            if (HasComp<DimensionVictimComponent>(entity))
                continue;

            if (Transform(entity).GridUid == null)
                continue;

            var totalDamage = damageable.TotalDamage.Float();

            if (totalDamage > highestDamage)
            {
                highestDamage = totalDamage;
                target = entity;
            }
        }

        return target;
    }

    private void ReturnToDimension(EntityUid uid, OldManComponent comp, bool cleanData = true)
    {

        comp.AnimationShown = false;
        comp.InDimension = true;

        if (cleanData == true)
        {
            comp.Target = null;

            var waitTime = _random.NextFloat(comp.MinWaitTime, comp.MaxWaitTime);
            comp.NextChaseStart = _timing.CurTime + TimeSpan.FromSeconds(waitTime);
        }

        _popup.PopupEntity("Оно начинает уходить под пол", uid, PopupType.Medium);

        Spawn(comp.DespawnAnimation, _trans.ToMapCoordinates((Transform(uid).Coordinates)));

        TeleportToDimension(uid, comp);

        Dirty(uid, comp);
    }

    private void TeleportToDimension(EntityUid uid, OldManComponent comp)
    {
        if (comp.DimensionUid != null)
        {
            var dimensionCoords = new EntityCoordinates(comp.DimensionUid.Value, new Vector2(1000, 1000));
            _trans.SetCoordinates(uid, dimensionCoords);
        }
    }

    private void OnMeleeHit(EntityUid uid, OldManComponent comp, ref MeleeHitEvent args)
    {
        if (!args.IsHit)
            return;

        if (args.HitEntities.Count == 0)
            return;

        if (comp.DimensionUid == null)
            return;

        var dimensionCoords = new EntityCoordinates(comp.DimensionUid.Value, new Vector2(0, 0));

        foreach (var target in args.HitEntities)
        {
            _trans.SetCoordinates(target, dimensionCoords);

            EnsureComp<DimensionVictimComponent>(target);
            EnsureComp<NoirOverlayComponent>(target);

            if (comp.DimensionTeleportSound != null)
                _audio.PlayPvs(comp.DimensionTeleportSound, target);
        }

        ReturnToDimension(uid, comp);
    }

}
