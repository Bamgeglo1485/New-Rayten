using Content.Shared.Access;
using Content.Shared.Access.Components;
using Content.Shared.Vanilla.Access.AlertLevelAccess;
using Content.Server.AlertLevel;
using Content.Server.Station.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server.Vanilla.Access.AlertLevelAccess;

public sealed class SharedAssSystem : EntitySystem
{
    [Dependency] private readonly StationSystem _station = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AlertLevelChangedEvent>(OnAlertChanged);
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
                foreach (var tag in extraAccessComp.AddedAccess)
                {
                    accessComp.Tags.Remove(tag);
                }
                extraAccessComp.AddedAccess.Clear();
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

    private void TryAddTags(HashSet<ProtoId<AccessLevelPrototype>> tags, AccessComponent accessComp, AlertLevelAccessComponent extraAccessComp)
    {
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
