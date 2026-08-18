using Content.Server.Radio.EntitySystems;
using Content.Server.CriminalRecords.Systems;
using Content.Shared.StationRecords;
using Content.Shared.Vanilla.Entities.DangerScanner;
using Content.Shared.Station;
using Content.Shared.Contraband;
using Content.Shared.Security;

namespace Content.Server.Vanilla.Entities.DangerScanner;

public sealed partial class DangerScannerSystem : SharedDangerScannerSystem
{
    [Dependency] private CriminalRecordsSystem _criminalRecords = default!;
    [Dependency] private SharedStationSystem _station = default!;
    [Dependency] private RadioSystem _radio = default!;

    //server-only
    protected override void SetWanted(EntityUid scanner, DangerScannerComponent component, string target, EntityUid item, ContrabandComponent contraband)
    {
        if (_station.GetOwningStation(scanner) is { } station)
        {
            var id = _criminalRecords.GetRecordByName(station, target);
            if (id != null)
            {
                var key = new StationRecordKey(id.Value, station);
                var reason = Loc.GetString("scanner-set-wanted", ("Severity", contraband.Severity), ("item", Name(item)));
                if (_criminalRecords.TryChangeStatus(key, SecurityStatus.Wanted, reason, Name(scanner)))
                {
                    _radio.SendRadioMessage(scanner,
                        Loc.GetString("scanner-radio-message", ("name", target), ("reason", reason)),
                        component.SecurityChannel, scanner);
                }
            }
        }
    }
    //client-only
    protected override void PlayScanAnimation(EntityUid uid, string scanLayer)
    {

    }
}
