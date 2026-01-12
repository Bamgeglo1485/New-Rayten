using Content.Server.AlertLevel;
using Content.Shared.Vanilla.Entities.ArsenalAuthorizator;
using Content.Shared.Vanilla.AlertKey;
using Content.Shared.Access.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server.Vanilla.Entities.ArsenalAuthorizator;

/// <inheritdoc/>
public sealed class ArsenalAuthorizatorSystem : SharedArsenalAuthorizatorSystem
{
    [Dependency] private readonly AlertLevelSystem _alertLevelSystem = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AlertLevelChangedEvent>(OnAlertChanged);
        SubscribeLocalEvent<ArsenalAuthorizatorComponent, MapInitEvent>(OnInit);
        SubscribeLocalEvent<ArsenalDoorComponent, MapInitEvent>(OnInit);
    }

    private void OnInit(EntityUid uid, ArsenalDoorComponent component, ref MapInitEvent args)
    {
        if (TryComp<AccessReaderComponent>(uid, out var reader))
            Access.AddDenyTag((uid, reader), component.BlockAccess);
    }

    private void OnInit(EntityUid uid, ArsenalAuthorizatorComponent component, ref MapInitEvent args)
    {
        var stationUid = StationSys.GetOwningStation(uid);
        if (stationUid == null)
            return;

        string alert = _alertLevelSystem.GetLevel(stationUid.Value);

        component.State = GetState(alert);

        SetDoors(component, stationUid.Value);
    }

    private void OnAlertChanged(AlertLevelChangedEvent args)
    {
        var query = EntityQueryEnumerator<ArsenalAuthorizatorComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (args.Station != StationSys.GetOwningStation(uid))
                continue;

            comp.State = GetState(args.AlertLevel);
            Appearance.SetData(uid, ArsenalAuthorizatorVisuals.State, (int)comp.State);
            Dirty(uid, comp);
            SetDoors(comp, args.Station);

            var uiState = new ArsenalAuthorizatorBoundInterfaceState();
            _ui.SetUiState(uid, ArsenalAuthorizatorUiKey.Key, uiState);
        }
    }

    private ArsenalAuthorizatorState GetState(string str)
    {
        return str switch
        {
            "red" => ArsenalAuthorizatorState.Red,
            "delta" => ArsenalAuthorizatorState.Gamma,
            "gamma" => ArsenalAuthorizatorState.Gamma,
            _ => ArsenalAuthorizatorState.Green
        };
    }

    protected override void ChangeAlertLevel(EntityUid uid, EntityUid stationUid, string reasonId)
    {
        if (!_proto.TryIndex<AlertLevelReasonPrototype>(reasonId, out var reasonproto))
            return;
        _alertLevelSystem.SetLevel(stationUid, reasonproto.Code, true, true, reason: reasonproto.Text);
    }
}
