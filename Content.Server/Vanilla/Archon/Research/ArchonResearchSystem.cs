using Content.Server.Research.Systems;
using Content.Server.NPC.Systems;
using Content.Server.Chat.Systems;
using Content.Shared.Chat;
using Content.Shared.Vanilla.Archon.Research;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Examine;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Utility;
using Robust.Shared.Timing;

namespace Content.Server.Vanilla.Archon.Research;

public sealed partial class ArchonBeaconSystem : SharedArchonResearchSystem
{
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedPowerReceiverSystem _power = default!;
    [Dependency] private ResearchSystem _research = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private MobStateSystem _mob = default!;

    private TimeSpan NextUpdate;

    public override void Initialize()
    {
        SubscribeLocalEvent<ArchonBeaconComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<ArchonComponent, ResearchAttemptEvent>(OnAttempt);

        SubscribeLocalEvent<ArchonComponent, ComponentShutdown>(OnArchonShutDown);
        SubscribeLocalEvent<ArchonBeaconComponent, ComponentShutdown>(OnBeaconShutDown);
        base.Initialize();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;

        if (now < NextUpdate)
            return;

        NextUpdate = now + TimeSpan.FromSeconds(5);

        var query = EntityQueryEnumerator<ArchonBeaconComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var beaconComp, out var beaconTrans))
        {
            if (!_power.IsPowered(uid) || !_research.TryGetClientServer(uid, out _, out _))
                continue;

            TryLinkBeaconToArchons((uid, beaconComp));
        }

        var archonQuery = EntityQueryEnumerator<ArchonComponent>();
        while (archonQuery.MoveNext(out var uid, out var archon))
        {
            if (archon.ResearchCoolDown == null)
                continue;

            if (now >= archon.NextResearchAt)
                ExtractResearchPoints((uid, archon));
        }
    }

    /// <summary>
    /// Проверки
    /// 1. Жив ли архонт
    /// это общие для всех архонтов проверки, специальные проверки нужно прописывать в отдельных системах для отдельных архонтов
    /// </summary>
    private void OnAttempt(EntityUid uid, ArchonComponent component, ResearchAttemptEvent args)
    {
        Transform(uid).Coordinates.TryDistance(EntityManager, Transform(args.Beacon.Owner).Coordinates, out var distance);
        if (distance > args.Beacon.Comp.Radius)
            args.Cancel();
    }

    private void OnExamine(EntityUid uid, ArchonBeaconComponent component, ExaminedEvent args)
    {
        if (!args.IsInDetailsRange || !_power.IsPowered(uid))
            return;

        if (component.LinkedArchons.Count == 0)
        {
            args.PushMarkup(Loc.GetString("archonbeacon-examine-no-links"));
            return;
        }

        using (args.PushGroup(nameof(ArchonBeaconComponent)))
        {
            args.PushMarkup(Loc.GetString("archonbeacon-examine-header"));
            foreach (var archon in component.LinkedArchons)
                args.PushMarkup(Loc.GetString("archonbeacon-examine-archon", ("archon", Name(archon))));
        }
    }

    private void OnArchonShutDown(EntityUid uid, ArchonComponent component, ref ComponentShutdown args)
    {
        if (TryComp<ArchonBeaconComponent>(component.LinkedBeacon, out var beacon))
            beacon.LinkedArchons.Remove(uid);
    }

    private void OnBeaconShutDown(EntityUid uid, ArchonBeaconComponent component, ref ComponentShutdown args)
    {
        foreach (var archon in component.LinkedArchons)
        {
            if (TryComp<ArchonComponent>(archon, out var archoncomp))
                archoncomp.LinkedBeacon = null;
        }
    }

    /// <summary>
    /// Связываем еще не связанных с маяком архонтов вокруг маяка с маяком
    /// </summary>
    public void TryLinkBeaconToArchons(Entity<ArchonBeaconComponent> beacon)
    {
        var archons = _lookup.GetEntitiesInRange<ArchonComponent>(
            Transform(beacon.Owner).Coordinates,
            beacon.Comp.Radius);

        foreach (var archon in archons)
        {
            // архонт уже связан с другим маяком
            if (archon.Comp.LinkedBeacon != null)
                continue;

            var ev = new ResearchAttemptEvent(beacon);
            RaiseLocalEvent(archon.Owner, ev);
            if (ev.Cancelled)
                continue;

            beacon.Comp.LinkedArchons.Add(archon.Owner);
            archon.Comp.LinkedBeacon = beacon.Owner;
            _chat.TrySendInGameICMessage(beacon.Owner, Loc.GetString("archonbeacon-link-complete", ("archon", Name(archon.Owner))), InGameICChatType.Speak, true);
            ExtractResearchPoints(archon);
        }
    }

    /// <summary>
    /// Выдаем очки за изучение архонта
    /// </summary>
    public override void ExtractResearchPoints(Entity<ArchonComponent> archon)
    {
        if (archon.Comp.LinkedBeacon == null)
            return;

        if (TryComp<MobStateComponent>(archon.Owner, out var mobState))
        {
            if (_mob.IsIncapacitated(archon.Owner, mobState))
                return;
        }

        if (!_research.TryGetClientServer(archon.Comp.LinkedBeacon.Value, out var server, out var serverComponent))
            return;

        var aPoints = archon.Comp.GetAPoints();
        var points = archon.Comp.GetPoints();
        _research.ModifyServerAdvancedPoints(server.Value, aPoints, serverComponent);
        _research.ModifyServerPoints(server.Value, points, serverComponent);
        _chat.TrySendInGameICMessage(
            archon.Comp.LinkedBeacon.Value,
            Loc.GetString("archonbeacon-extract-points",
                    ("apoints", aPoints),
                    ("points", points)),
            InGameICChatType.Speak, true);

        if (archon.Comp.ResearchCoolDown != null)
            archon.Comp.NextResearchAt = _timing.CurTime + archon.Comp.ResearchCoolDown.Value;
    }
}
