using Content.Server.CriminalRecords.Systems;
using Content.Shared.IdentityManagement;
using Content.Server.Radio.EntitySystems;
using Content.Shared.Vanilla.Dominator;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Damage.Components;
using Content.Shared.Security;
using Content.Shared.StationRecords;
using Content.Shared.Station;
using Robust.Shared.Containers;
using System.Linq;

namespace Content.Server.Vanilla.Dominator;

public sealed partial class SecuritronSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _container = default!;
    // [Dependency] private SharedStationRecordsSystem _records = default!;
    // [Dependency] private CriminalRecordsSystem _criminalRecords = default!;
    // [Dependency] private SharedStationSystem _station = default!;
    // [Dependency] private RadioSystem _radio = default!;
    public override void Initialize()
    {
        base.Initialize();
        // SubscribeLocalEvent<SecuritronComponent, DamageChangedEvent>(OnDamageChange);
        SubscribeLocalEvent<SecuritronComponent, ComponentInit>(OnSecuritronInit);
    }
    private void OnSecuritronInit(EntityUid uid, SecuritronComponent component, ComponentInit args)
    {
        //инициализируем контейнер
        component.HandCuffContainer = _container.EnsureContainer<ContainerSlot>(uid, "HandCuffContainer");

        var spawned = Spawn("Handcuffs", Transform(uid).Coordinates);

        _container.Insert(spawned, component.HandCuffContainer);
    }


    // private void OnDamageChange(EntityUid uid, SecuritronComponent component, DamageChangedEvent args)
    // {
    //     if (args.Origin == null)
    //         return;

    //     var source = args.Origin.Value;

    //     if (!args.DamageIncreased
    //     || args.DamageDelta == null
    //     || args.DamageDelta.GetTotal() <= 0
    //     || !TryComp<DangerMobComponent>(source, out var sourcecomp)
    //     || source == uid || sourcecomp.MaxDanger)
    //     {
    //         return;
    //     }
    //     var targetname = Identity.Name(source, EntityManager);

    //     if (_station.GetOwningStation(uid) is { } station)
    //     {
    //         var id = _records.GetRecordByName(station, targetname);
    //         if (id != null)
    //         {
    //             var key = new StationRecordKey(id.Value, station);
    //             var reason = Loc.GetString("securitron-set-wanted");
    //             if (_criminalRecords.TryChangeStatus(key, SecurityStatus.Wanted, reason, targetname))
    //             {
    //                 _radio.SendRadioMessage(uid,
    //                     Loc.GetString("securitron-set-wanted-radio-message", ("name", targetname), ("reason", reason)),
    //                     component.SecurityChannel, uid);
    //             }
    //         }
    //         else
    //         {
    //             sourcecomp.MaxDanger = true;
    //             source.SpawnTimer(TimeSpan.FromSeconds(15), () => sourcecomp.MaxDanger = false);
    //         }
    //     }
    // }
}
