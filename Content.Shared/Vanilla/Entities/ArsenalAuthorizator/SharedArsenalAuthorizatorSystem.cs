using Content.Shared.Access.Systems;
using Content.Shared.Access.Components;
using Content.Shared.Station;
using Robust.Shared.Physics.Events;
namespace Content.Shared.Vanilla.Entities.ArsenalAuthorizator;

public abstract partial class SharedArsenalAuthorizatorSystem : EntitySystem
{

    [Dependency] protected readonly SharedStationSystem StationSys = default!;
    [Dependency] protected readonly AccessReaderSystem Access = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ArsenalAuthorizatorComponent, ArsenalAuthorizatorOpenMessage>(OnOpen);
        SubscribeLocalEvent<ArsenalDoorComponent, PreventCollideEvent>(PreventCollide);
    }

    private void PreventCollide(EntityUid uid, ArsenalDoorComponent component, ref PreventCollideEvent args)
    {
        if (args.Cancelled)
            return;

        if (!Access.IsAllowed(args.OtherEntity, uid))
            return;

        args.Cancelled = true;
    }

    private void OnOpen(Entity<ArsenalAuthorizatorComponent> ent, ref ArsenalAuthorizatorOpenMessage args)
    {
        var stationUid = StationSys.GetOwningStation(ent.Owner);
        if (stationUid == null)
            return;

        if (!IsUserAllowedAccess(ent, args.Actor))
            return;

        ent.Comp.IsOpen = true;
        Dirty(ent);

        ChangeAlertLevel(ent.Owner, stationUid.Value, args.ReasonId);
        SetDoors(ent.Comp, stationUid.Value);

        if (_ui.TryGetOpenUi(ent.Owner, ArsenalAuthorizatorUiKey.Key, out var bui))
            bui.Update();
    }

    public void SetDoors(ArsenalAuthorizatorComponent comp, EntityUid stationUid)
    {
        var query = EntityQueryEnumerator<ArsenalDoorComponent, AccessReaderComponent>();
        while (query.MoveNext(out var door, out var doorComp, out var accesReaderComp))
        {
            Log.Info($"зырим {door}");
            if (stationUid != StationSys.GetOwningStation(door))
                continue;

            if (comp.IsOpen)
            {
                Log.Info("удаляем доступ");
                Access.RemoveDenyTag((door, accesReaderComp), doorComp.BlockAccess);
            }
            else
            {
                Log.Info("добавляем доступ");
                Access.AddDenyTag((door, accesReaderComp), doorComp.BlockAccess);
            }
        }
    }

    public bool IsUserAllowedAccess(Entity<ArsenalAuthorizatorComponent> ent, EntityUid user)
    {
        if (ent.Comp.IsOpen)
            return false;

        if (Access.IsAllowed(user, ent))
            return true;

        // _popup.PopupClient(Loc.GetString("turret-controls-access-denied"), ent, user);
        // _audio.PlayPredicted(ent.Comp.AccessDeniedSound, ent, user);

        return false;
    }

    protected virtual void ChangeAlertLevel(EntityUid uid, EntityUid stationUid, string reasonId)
    {

    }
}