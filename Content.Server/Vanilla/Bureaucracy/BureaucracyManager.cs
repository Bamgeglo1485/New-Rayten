using Robust.Shared.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Content.Shared.Vanilla.Bureaucracy;
using Content.Shared.Paper;
using Robust.Shared.Prototypes;
using Content.Server.Station.Systems;
using Content.Server.Station.Components;
using Content.Server.Mind;
using Content.Server.Roles;
using Content.Server.Roles.Jobs;




namespace Content.Server.Vanilla.Bureaucracy;

public sealed class BureaucracyManager : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly PaperSystem  _paperSystem  = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly JobSystem _jobs = default!;
    [Dependency] private readonly MindSystem _minds = default!;

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
        var Playerent = args.SenderSession.AttachedEntity;
        if (!TryComp<PaperComponent>(paperUid, out var paperComp) || HasComp<StampComponent>(paperUid))
            return;

        _audio.PlayPvs(paperComp.Sound, paperUid);

        string text = Loc.GetString(prototype.Text, 
                                    ("station", getstationname(paperUid)), 
                                    ("label", prototype.label), 
                                    ("name", getname(Playerent)), 
                                    ("job", getjob(Playerent)), 
                                    ("date", getdate())
                                    );

        _paperSystem.SetContent(new Entity<PaperComponent>(paperUid, paperComp), text);
    }

    private string getstationname(EntityUid paperUid)
    {
        var stations = _station.GetStations();

        foreach (var stationUid in stations)
        {
            if(!TryComp<StationDataComponent>(stationUid, out var stationData))
                continue;

            var largestGrid = _station.GetLargestGrid(stationData);
            var grid = Transform(paperUid).GridUid;

            if (grid.HasValue && largestGrid == grid.Value)
                return MetaData(stationUid).EntityName;

        }
        return "Номер станции";
    }

    private string getname(EntityUid? Playerent) 
    {
        if (Playerent == null || !TryComp<MetaDataComponent>(Playerent.Value, out var metaData))
            return "Составитель документа";

        return metaData.EntityName;
    }


    private string getjob(EntityUid? Playerent)
    {
        if (Playerent == null)
            return "Без должности";

        if (!_minds.TryGetMind(Playerent.Value, out var mindId, out var mind))
            return "Без должности";

        if (_jobs.MindTryGetJobName(mindId, out var jobName))
            return jobName; 

        return "Без должности";
    }
    private string getdate()
    {
        var now = DateTime.Now;
        return $"{now:dd.MM}.{now.Year + 1000}";
    }

}
