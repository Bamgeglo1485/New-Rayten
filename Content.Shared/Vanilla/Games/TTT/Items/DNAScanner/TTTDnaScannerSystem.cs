using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Pinpointer;
using Content.Shared.Popups;
using Robust.Shared.Timing;

namespace Content.Shared.Vanilla.Games.TTT.Items.DNAScanner;

public sealed partial class TTTDnaScannerSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedPinpointerSystem _pin = default!;
    [Dependency] private MetaDataSystem _meta = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    private float _accumulator = 0;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TTTDeadBodyComponent, InteractUsingEvent>(OnInteract);
        SubscribeLocalEvent<NameOverlayComponent, TTTDisguiserActionEvent>(OnDisguiser);
        SubscribeLocalEvent<TTTDecoyComponent, ComponentShutdown>(OnDecoyShutdown);
        SubscribeLocalEvent<TTTDecoyComponent, UseInHandEvent>(OnUseInHand);
    }

    public override void Update(float frameTime)
    {
        _accumulator += frameTime;
        if (_accumulator < 1f)
            return;
        _accumulator = 0;
        base.Update(frameTime);
        var query = EntityQueryEnumerator<TTTDeadBodyComponent>();
        while (query.MoveNext(out var uid, out var body))
        {
            if (body.Killer == null)
                continue;
            if (_timing.CurTime - body.DeathTime > TimeSpan.FromMinutes(1))
                body.Killer = null;
        }
    }
    private void OnUseInHand(EntityUid uid, TTTDecoyComponent decoy, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;
        if (decoy.RedirectedBy != null)
            return;

        decoy.RedirectedBy = args.User;
        if (TryComp<OnDnaScannerComponent>(args.User, out var onDna))
        {
            onDna.Redirect = uid;
            foreach (var scanner in onDna.Scanners)
                _pin.SetTarget(scanner, uid);
        }

        args.Handled = true;
    }
    private void OnDecoyShutdown(EntityUid uid, TTTDecoyComponent decoy, ref ComponentShutdown args)
    {
        if (!TryComp<OnDnaScannerComponent>(decoy.RedirectedBy, out var onDna))
            return;
        if (onDna.Redirect != uid)
            return;
        onDna.Redirect = null;
        if (onDna.InStealth)
            return;

        foreach (var scanner in onDna.Scanners)
            _pin.SetTarget(scanner, decoy.RedirectedBy);
    }
    private void OnDisguiser(EntityUid uid, NameOverlayComponent component, TTTDisguiserActionEvent args)
    {
        if (args.Handled)
            return;
        (component.OldName, component.Name) = (component.Name, component.OldName);
        Dirty(uid, component);
        _meta.SetEntityName(uid, component.Name);

        if (TryComp<OnDnaScannerComponent>(uid, out var onDna))
        {
            onDna.InStealth = !onDna.InStealth;
            if (onDna.Redirect == null && onDna.InStealth)
            {
                foreach (var scanner in onDna.Scanners)
                    _pin.SetTarget(scanner, null);
            }
        }
        args.Handled = true;
    }

    private void OnInteract(EntityUid uid, TTTDeadBodyComponent body, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;
        if (TryComp<TTTMarkerComponent>(args.User, out var marker) && marker.Role != TTTRole.Detective)
        {
            _popup.PopupEntity("Вы не умеете этим пользоваться", args.User, PopupType.LargeCaution);
            return;
        }
        if (body.Killer == null || HasComp<TTTNoDnaComponent>(uid))
        {
            _popup.PopupEntity("ДНК не сохранились на теле жертвы", args.User, PopupType.LargeCaution);
            return;
        }
        TrySetTarget(uid, body.Killer.Value);
        args.Handled = true;
    }

    public void TrySetTarget(EntityUid scanner, EntityUid target)
    {
        if (!HasComp<TTTDnaScannerComponent>(scanner))
            return;
        if (!TryComp<OnDnaScannerComponent>(target, out var onScaner))
            return;
        if (!TryComp<PinpointerComponent>(scanner, out var pin))
            return;

        if (TryComp<OnDnaScannerComponent>(pin.Target, out var oldTargetDna))
            oldTargetDna.Scanners.Remove(scanner);

        onScaner.Scanners.Add(scanner);
        if (onScaner.Redirect != null)
        {
            _pin.SetTarget(scanner, onScaner.Redirect);
        }
        else
        {
            if (!onScaner.InStealth)
                _pin.SetTarget(scanner, target);
        }
    }
}
