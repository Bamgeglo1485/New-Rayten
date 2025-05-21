using Content.Server.GameTicking;
using Content.Shared.Vanilla.TDMRoundEnd;
using Content.Shared.Vanilla.CCVars;
using Robust.Shared.Configuration;
using Robust.Shared.Map.Components;
using Robust.Shared.Map;
namespace Content.Server.Vanilla.TDM;

public sealed class TDMRoundEndSystem : EntitySystem
{
    private bool _isEnabled = false;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    public override void Initialize()
    {
        base.Initialize();
        _cfg.OnValueChanged(CCVVars.TDMRoundEndEnabled, v => _isEnabled = v, true);
        SubscribeLocalEvent<RoundEndTextAppendEvent>(OnRoundEnded);
    }
    private void OnRoundEnded(RoundEndTextAppendEvent ev)
    {
        if (!_isEnabled)
            return;
        var uid = Spawn("TeamDeathMatch", MapCoordinates.Nullspace);
        if (TryComp<TDMRuleComponent>(uid, out var rule))
        {
            rule.OnlyOneCycle = true;
            RaiseLocalEvent(uid, new NewTDMCycleEvent());
        }
    }
}
