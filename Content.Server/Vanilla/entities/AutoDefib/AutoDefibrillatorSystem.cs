using Content.Shared.Atmos.Rotting;
using Content.Shared.Electrocution;
using Content.Shared.Damage.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Mobs;
using Content.Shared.Implants.Components;
using Content.Shared.Interaction;
using Robust.Shared.Audio.Systems;

namespace Content.Server.Vanilla.entities.AutoDefib;

public sealed class AutoDefibrillatorSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedElectrocutionSystem _electrocution = default!;
    [Dependency] private readonly SharedRottingSystem _rotting = default!;
    [Dependency] private readonly MobThresholdSystem _threshold = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedInteractionSystem _interactionSystem = default!;
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
            if (dmg.Damage.GetTotal() >= amountToAlive)
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
