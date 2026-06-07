using Content.Shared.Administration;
using Content.Shared.Vanilla.Background;
using Content.Shared.Vanilla.TDM;
using Robust.Shared.Prototypes;

namespace Content.Server.Vanilla.Background;

public sealed partial class BackGroundSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private AdminFrozenSystem _freeze = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AwaitBackgroundComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<AwaitBackgroundComponent, ComponentShutdown>(OnShutdown);
        SubscribeNetworkEvent<TakeGhostBackgroundEvent>(OnTakeGhostBackgroundEvent);
    }
    private void OnMapInit(EntityUid uid, AwaitBackgroundComponent component, MapInitEvent args)
    {
        _freeze.FreezeAndMute(uid);
    }

    private void OnShutdown(EntityUid uid, AwaitBackgroundComponent component, ComponentShutdown args)
    {
        if (!HasComp<TDMMarkerComponent>(uid))
            RemComp<AdminFrozenComponent>(uid);

    }

    private void OnTakeGhostBackgroundEvent(TakeGhostBackgroundEvent msg, EntitySessionEventArgs args)
    {
        if (!args.SenderSession.AttachedEntity.HasValue)
            return;

        var uid = args.SenderSession.AttachedEntity.Value;

        if (!HasComp<AwaitBackgroundComponent>(uid))
            return;

        RemComp<AwaitBackgroundComponent>(uid);

        if (_prototype.TryIndex(msg.Background, out var bgProto))
        {
            ApplySpecials(uid, bgProto.Specials);

            var backgroundcomp = EnsureComp<BackgroundComponent>(uid);
            backgroundcomp.GeneralBackground = msg.Background;
            Dirty(uid, backgroundcomp);
        }
        else
        {
            Log.Error($"Не удалось найти предысторию с ID {msg.Background}");
        }
    }
    private void ApplySpecials(EntityUid uid, List<BackgroundSpecial> specials)
    {
        foreach (var special in specials)
            special.Apply(uid);
    }
}
