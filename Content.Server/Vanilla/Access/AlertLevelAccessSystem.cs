using Content.Shared.Access;
using Content.Shared.Access.Components;
using Content.Shared.Vanilla.Access.AlertLevelAccess;
using Content.Shared.Vanilla.Dominator;
using Content.Shared.Examine;
using Content.Server.AlertLevel;
using Content.Server.Station.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server.Vanilla.Access.AlertLevelAccess;

public sealed class SharedAssSystem : EntitySystem
{
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly DangerMobSystem _dangermob = default!;
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AlertLevelChangedEvent>(OnAlertChanged);
    }
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<AlertLevelAccessComponent, AccessComponent>();
        while (query.MoveNext(out var uid, out var extraAccessComp, out var accessComp))
        {
            extraAccessComp.Timer += frameTime;

            if (extraAccessComp.Timer < extraAccessComp.CheckDelay)
                continue;

            extraAccessComp.Timer = 0;

            if (extraAccessComp.AddedByCode)
                continue;

            if (ExtraAccess(uid, extraAccessComp))
            {
                TryAddTags(extraAccessComp.Red, accessComp, extraAccessComp);
            }
            else
            {
                RemoveExtraAccess(extraAccessComp, accessComp);
            }
            Dirty(uid, accessComp);
        }
    }

    private bool ExtraAccess(EntityUid uid, AlertLevelAccessComponent alert)
    {
        var ents = _lookup.GetEntitiesInRange(uid, alert.ScanRange, LookupFlags.Dynamic | LookupFlags.Approximate);

        int maxdanger = 0;

        foreach (var target in ents)
        {
            if (target == uid)
                continue;

            //если цель за стеной - игнорируем
            if (!_examine.InRangeUnOccluded(uid, target, 10f, ignoreInsideBlocker: false))
                continue;

            //считаем опасность цели
            int targetdanger = _dangermob.GetEntityDanger(target);

            if (targetdanger > maxdanger)
                maxdanger = targetdanger;
        }
        return maxdanger == 10;
    }

    private void OnAlertChanged(AlertLevelChangedEvent args)
    {
        var query = EntityQueryEnumerator<AlertLevelAccessComponent, AccessComponent>();
        while (query.MoveNext(out var uid, out var extraAccessComp, out var accessComp))
        {
            if (args.Station != _station.GetOwningStation(uid))
                continue;

            if (args.AlertLevel == extraAccessComp.ResetOnLevel)
            {
                RemoveExtraAccess(extraAccessComp, accessComp);
                extraAccessComp.AddedByCode = false;
            }

            switch (args.AlertLevel)
            {
                case "red":
                    TryAddTags(extraAccessComp.Red, accessComp, extraAccessComp);
                    break;
                case "blue":
                    TryAddTags(extraAccessComp.Blue, accessComp, extraAccessComp);
                    break;
                case "gamma":
                    TryAddTags(extraAccessComp.Gamma, accessComp, extraAccessComp);
                    break;
                case "delta":
                    TryAddTags(extraAccessComp.Delta, accessComp, extraAccessComp);
                    break;
            }
            Dirty(uid, accessComp);
        }
    }
    private void RemoveExtraAccess(AlertLevelAccessComponent extraAccessComp, AccessComponent accessComp)
    {
        foreach (var tag in extraAccessComp.AddedAccess)
        {
            accessComp.Tags.Remove(tag);
        }
        extraAccessComp.AddedAccess.Clear();
    }
    private void TryAddTags(HashSet<ProtoId<AccessLevelPrototype>> tags, AccessComponent accessComp, AlertLevelAccessComponent extraAccessComp)
    {
        extraAccessComp.AddedByCode = true;
        foreach (var tag in tags)
        {
            if (!accessComp.Tags.Contains(tag))
            {
                accessComp.Tags.Add(tag);
                extraAccessComp.AddedAccess.Add(tag);
            }
        }
    }

}
