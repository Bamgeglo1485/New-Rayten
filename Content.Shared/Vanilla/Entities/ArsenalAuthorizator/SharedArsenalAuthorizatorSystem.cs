using Content.Shared.Access.Systems;
using Content.Shared.Access.Components;
using Content.Shared.Station;
using Content.Shared.Nuke;
using Content.Shared.Interaction;
using Robust.Shared.Physics.Events;
using Robust.Shared.GameObjects;
using Robust.Shared.Audio.Systems;
using System.Diagnostics;

namespace Content.Shared.Vanilla.Entities.ArsenalAuthorizator;

public abstract partial class SharedArsenalAuthorizatorSystem : EntitySystem
{
    [Dependency] protected readonly SharedStationSystem StationSys = default!;
    [Dependency] protected readonly SharedAppearanceSystem Appearance = default!;
    [Dependency] protected readonly AccessReaderSystem Access = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedPointLightSystem _lights = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ArsenalAuthorizatorComponent, ArsenalAuthorizatorOpenMessage>(OnOpen);
        SubscribeLocalEvent<ArsenalAuthorizatorComponent, InteractUsingEvent>(OnNukeUse);
        SubscribeLocalEvent<ArsenalDoorComponent, PreventCollideEvent>(PreventCollide);
    }

    private void PreventCollide(EntityUid uid, ArsenalDoorComponent component, ref PreventCollideEvent args)
    {
        if (args.Cancelled)
            return;

        if (!Access.IsAllowed(args.OtherEntity, uid))
        {
            _audio.PlayPredicted(component.AccessDeniedSound, uid, args.OtherEntity);
            return;
        }


        args.Cancelled = true;
    }

    private void OnNukeUse(Entity<ArsenalAuthorizatorComponent> ent, ref InteractUsingEvent args)
    {
        if (!HasComp<NukeDiskComponent>(args.Used))
            return;

        var stationUid = StationSys.GetOwningStation(ent.Owner);
        if (stationUid == null)
            return;

        ChangeAlertLevel(ent.Owner, stationUid.Value, ent.Comp.NukeDiscAlertReason);
    }

    private void OnOpen(Entity<ArsenalAuthorizatorComponent> ent, ref ArsenalAuthorizatorOpenMessage args)
    {
        var stationUid = StationSys.GetOwningStation(ent.Owner);
        if (stationUid == null)
            return;

        if (!IsUserAllowedAccess(ent, args.Actor))
            return;

        ent.Comp.State = ArsenalAuthorizatorState.Red;
        Appearance.SetData(ent.Owner, ArsenalAuthorizatorVisuals.State, (int)ent.Comp.State);
        _lights.SetColor(ent.Owner, GetColor(ent.Comp.State));


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
            if (stationUid != StationSys.GetOwningStation(door))
                continue;

            switch (comp.State)
            {
                case ArsenalAuthorizatorState.Green:
                    Access.AddDenyTag((door, accesReaderComp), doorComp.BlockAccess);
                    break;
                case ArsenalAuthorizatorState.Red:
                    Access.RemoveDenyTag((door, accesReaderComp), doorComp.BlockAccess);
                    break;
                case ArsenalAuthorizatorState.Gamma:
                    RemComp<AccessReaderComponent>(door);
                    break;
            }
            Appearance.SetData(door, ArsenalAuthorizatorVisuals.State, (int)comp.State);
            _lights.SetColor(door, GetColor(comp.State));
        }
    }

    public bool IsUserAllowedAccess(Entity<ArsenalAuthorizatorComponent> ent, EntityUid user)
    {
        if (ent.Comp.State is ArsenalAuthorizatorState.Red or ArsenalAuthorizatorState.Gamma)
            return false;

        if (Access.IsAllowed(user, ent))
            return true;

        return false;
    }
    public Color GetColor(ArsenalAuthorizatorState state)
    {
        return state switch
        {
            ArsenalAuthorizatorState.Green => Color.FromHex("#33e633"),
            ArsenalAuthorizatorState.Red => Color.FromHex("#da2a2a"),
            ArsenalAuthorizatorState.Gamma => Color.FromHex("#DB7093"),
            _ => Color.White
        };
    }
    protected virtual void ChangeAlertLevel(EntityUid uid, EntityUid stationUid, string reasonId)
    {

    }
}