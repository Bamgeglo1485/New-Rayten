using Content.Shared.Atmos.Rotting;
using Content.Shared.Electrocution;
using Content.Shared.Damage.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Mobs;
using Content.Shared.Implants.Components;
using Content.Shared.Interaction;
using Content.Shared.Damage.Systems;
using Robust.Shared.Audio.Systems;

namespace Content.Server.Vanilla.entities.AutoDefib;

public sealed partial class AutoDefibrillatorSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedElectrocutionSystem _electrocution = default!;
    [Dependency] private SharedRottingSystem _rotting = default!;
    [Dependency] private MobThresholdSystem _threshold = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedInteractionSystem _interactionSystem = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    private const float Delay = 3f;
    private float _timer;
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _timer += frameTime;

        if (_timer < Delay)
            return;
        _timer = 0f;

        var query = EntityQueryEnumerator<MobStateComponent, DamageableComponent, MobThresholdsComponent, AutoDefibrillatorComponent>();
        while (query.MoveNext(out var uid, out var mob, out var dmg, out var threshhold, out var autodefib))
        {
            if (!_mobState.IsDead(uid, mob))
                continue;

            if (_rotting.IsRotten(uid))
                continue;

            var amountToAlive = _threshold.GetThresholdForState(uid, MobState.Critical, threshhold);
            if (_damageable.GetTotalDamage((uid, dmg)) >= amountToAlive)
                continue;

            _audio.PlayPvs(autodefib.ZapSound, uid);
            _electrocution.TryDoElectrocution(uid, uid, autodefib.ZapDamage, autodefib.WritheDuration, true, ignoreInsulation: true);
            _mobState.ChangeMobState(uid, MobState.Critical, mob, uid);
            HashSet<EntityUid> interacters = [];
            _interactionSystem.GetEntitiesInteractingWithTarget(uid, interacters);

            foreach (var other in interacters)
            {
                if (other != uid)
                    _electrocution.TryDoElectrocution(uid, null, autodefib.ZapDamage, autodefib.WritheDuration, true);
            }
        }
    }

}
