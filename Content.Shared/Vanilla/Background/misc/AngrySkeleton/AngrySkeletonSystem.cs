using Content.Shared.Examine;

namespace Content.Server.Vanilla.Background.AngrySkeleton;

public sealed class AngrySkeletonSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<AngrySkeletonComponent, ExaminedEvent>(OnExaminedEvent);
    }

    private void OnExaminedEvent(EntityUid uid, AngrySkeletonComponent component, ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("[color=Red]Пустые глазницы сверкают злобой, а кости дрожат от ярости. Он явно не намерен обсуждать философию — только ломать, крушить и грызть.[/color]"));
    }
}
