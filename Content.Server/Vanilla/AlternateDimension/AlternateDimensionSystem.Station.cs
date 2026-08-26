using Content.Server.Station.Components;
using Content.Server.Station.Events;
using Content.Shared.Station.Components;
using Content.Shared.AlternateDimension;
using Robust.Shared.Random;

namespace Content.Server.AlternateDimension;

public sealed partial class AlternateDimensionSystem
{
    private void InitializeStation()
    {
        SubscribeLocalEvent<StationAlternateDimensionGeneratorComponent, StationPostInitEvent>(OnStationInit);
    }

    private void OnStationInit(Entity<StationAlternateDimensionGeneratorComponent> ent, ref StationPostInitEvent args)
    {
        if (!TryComp<StationDataComponent>(ent, out var stationData))
            return;

        var prototypeId = _random.Pick(ent.Comp.Dimensions);

        if (!_prototypeManager.TryIndex(prototypeId, out var prototype))
        {
            Log.Warning($"Failed to find prototype {prototypeId} for station alternate dimension generation.");
            return;
        }

        var alterParams = new AlternateDimensionParams
        {
            Seed = _random.Next(),
            Dimension = prototype,
        };

        var stationGrid = _stationSystem.GetLargestGrid((ent, stationData));

        if (stationGrid is null)
            return;

        MakeAlternativeRealityGrid(stationGrid.Value, alterParams);
    }
}
