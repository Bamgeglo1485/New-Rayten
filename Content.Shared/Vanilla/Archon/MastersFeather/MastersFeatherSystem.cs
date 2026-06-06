using Content.Shared.DoAfter;
using Content.Shared.Paper;
using Content.Shared.Interaction;
using Content.Shared.Vanilla.Archon.Research;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Serialization;
using Robust.Shared.Player;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using System.Text;

namespace Content.Shared.Vanilla.Archon.MastersFeather;

public sealed partial class MastersFeatherSystem : EntitySystem
{
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private PaperSystem _paper = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedArchonResearchSystem _archon = default!;
    [Dependency] private IGameTiming _timing = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MastersFeatherComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<MastersFeatherComponent, MastersFeatherEvent>(OnDoAfter);
    }

    private void OnAfterInteract(EntityUid uid, MastersFeatherComponent component, AfterInteractEvent args)
    {
        if (args.Handled)
            return;
        if (!args.CanReach)
            return;
        if (args.Target is not { } target)
            return;

        if (!TryComp<PaperComponent>(target, out var paper))
            return;

        if (paper.StampedBy.Count > 0)
            return;

        var doAfterArgs = new DoAfterArgs(
            EntityManager,
            args.User,
            component.DoAfterDuration,
            new MastersFeatherEvent(),
            uid,
            target: target,
            used: uid
        )
        {
            DistanceThreshold = 2f
        };
        if (_doAfter.TryStartDoAfter(doAfterArgs))
        {
            if (_timing.IsFirstTimePredicted)
                component.AudioStream = _audio.PlayPredicted(component.WritingSound, args.User, args.User)?.Entity;
            args.Handled = true;
        }
    }

    private void OnDoAfter(EntityUid uid, MastersFeatherComponent component, DoAfterEvent args)
    {
        component.AudioStream = _audio.Stop(component.AudioStream);
        if (args.Cancelled || args.Handled || args.Args.Target == null)
            return;
        var target = args.Args.Target.Value;
        var user = args.Args.User;

        _audio.PlayPredicted(component.DoneSound, target, user);

        if (!TryComp<PaperComponent>(target, out var paper))
            return;

        if (paper.StampedBy.Count > 0)
            return;

        if (!TryComp<ActorComponent>(user, out var actor))
            return;

        var text = GenerateBiography(actor.PlayerSession.UserId, component);

        _paper.SetContent((target, paper), text);
        if (!component.UsedBy.Contains(user))
        {
            if (TryComp<ArchonComponent>(uid, out var archon))
                _archon.ExtractResearchPoints((uid, archon));
            component.UsedBy.Add(user);
        }
        args.Handled = true;
    }

    private string GenerateBiography(NetUserId userId, MastersFeatherComponent comp)
    {
        var seed = GetStableSeed(userId);
        var random = new System.Random(seed);

        var sb = new StringBuilder();

        foreach (var datasetId in comp.BiographyDatasets)
        {
            var dataset = _proto.Index(datasetId);
            var values = dataset.Values;

            if (values.Count == 0)
                continue;

            var key = values[random.Next(values.Count)];
            sb.AppendLine(Loc.GetString(key));
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private int GetStableSeed(NetUserId userId)
    {
        int hash = 5381;
        foreach (var c in userId.ToString())
            hash = (hash << 5) + hash + c;

        return hash;
    }
}

[Serializable, NetSerializable]
public sealed partial class MastersFeatherEvent : SimpleDoAfterEvent
{
}
