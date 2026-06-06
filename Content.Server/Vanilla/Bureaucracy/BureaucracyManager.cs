using Content.Server.Station.Systems;
using Content.Server.Mind;
using Content.Server.Roles.Jobs;
using Content.Shared.Vanilla.Bureaucracy;
using Content.Shared.Paper;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server.Vanilla.Bureaucracy;

public sealed partial class BureaucracyManager : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private PaperSystem _paperSystem = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private StationSystem _station = default!;
    [Dependency] private JobSystem _jobs = default!;
    [Dependency] private MindSystem _minds = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<RequestWriteOnDockEvent>(WriteOnDock);
    }

    private void WriteOnDock(RequestWriteOnDockEvent msg, EntitySessionEventArgs args)
    {
        if (!_prototypeManager.TryIndex<BureaucracyDocumentPrototype>(msg.id, out var prototype))
        {
            Log.Warning($"Прототип с ID {msg.id} не найден.");
            return;
        }

        var paperUid = GetEntity(msg.paper);
        var playerent = args.SenderSession.AttachedEntity;
        if (!TryComp<PaperComponent>(paperUid, out var paperComp))
            return;

        if (paperComp.StampedBy.Count > 0)
            return;

        _audio.PlayPvs(paperComp.Sound, paperUid);

        string text = Loc.GetString(prototype.Text,
                                    ("station", Getstationname(paperUid)),
                                    ("label", prototype.Label),
                                    ("name", GetName(playerent)),
                                    ("job", Getjob(playerent)),
                                    ("date", Getdate())
                                    );

        _paperSystem.SetContent(new Entity<PaperComponent>(paperUid, paperComp), text);
    }


    private string Getstationname(EntityUid paperUid)
    {
        var stations = _station.GetStations();

        foreach (var stationUid in stations)
        {
            var largestGrid = _station.GetLargestGrid(stationUid);
            var grid = Transform(paperUid).GridUid;

            if (grid.HasValue && largestGrid == grid.Value)
                return MetaData(stationUid).EntityName;

        }
        return "Номер станции";
    }

    private string GetName(EntityUid? playerEnt)
    {
        return playerEnt is { } uid
            ? MetaData(uid).EntityName
            : "Составитель документа";
    }


    private string Getjob(EntityUid? playerent)
    {
        if (playerent == null)
            return "Без должности";

        if (!_minds.TryGetMind(playerent.Value, out var mindId, out var mind))
            return "Без должности";

        if (_jobs.MindTryGetJobName(mindId, out var jobName))
            return jobName;

        return "Без должности";
    }
    private string Getdate()
    {
        var now = DateTime.Now;
        return $"{now:dd.MM}.{now.Year + 1000}";
    }

}
