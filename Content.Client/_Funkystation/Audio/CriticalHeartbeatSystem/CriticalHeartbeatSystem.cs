using Content.Shared.Damage.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Client.Audio;
using Robust.Shared.Audio;
using AudioComponent = Robust.Shared.Audio.Components.AudioComponent;
using Robust.Shared.Timing;
using Robust.Shared.Player;

namespace Content.Client._Funkystation.Audio
{
    public sealed class CriticalHeartbeatSystem : EntitySystem
    {
        [Dependency] private readonly AudioSystem _audio = default!;
        [Dependency] private readonly IGameTiming _timing = default!;
        [Dependency] private readonly MobThresholdSystem _mobThresholdSystem = default!;

        private EntityUid? _trackedEntity;
        private bool _playing;
        private TimeSpan _startTime;
        private TimeSpan _elapsed;
        private TimeSpan _nextTickTime;
        private TimeSpan _currentInterval;
        private TimeSpan _minInterval = TimeSpan.FromSeconds(0.3);
        private TimeSpan _initialInterval = TimeSpan.FromSeconds(1);
        private TimeSpan _accelDuration = TimeSpan.FromSeconds(12.0);
        private float _startPitch = 1.0f;
        private float _maxPitch = 1.25f;
        private (EntityUid Entity, AudioComponent Component)? _playingAudio;

        private readonly ResolvedPathSpecifier _heartbeat = new("/Audio/_Funkystation/Effects/heartbeat.ogg");

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<LocalPlayerAttachedEvent>(OnPlayerAttach);
            SubscribeLocalEvent<LocalPlayerDetachedEvent>(OnPlayerDetach);
            SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
        }

        private void OnPlayerAttach(LocalPlayerAttachedEvent args)
        {
            _trackedEntity = args.Entity;
            if (EntityManager.TryGetComponent(_trackedEntity, out MobStateComponent? mob) && mob.CurrentState == MobState.Critical)
            {
                StartHeartbeat();
            }
        }

        private void OnPlayerDetach(LocalPlayerDetachedEvent args)
        {
            StopHeartbeat();
            _trackedEntity = null;
        }

        private void OnMobStateChanged(MobStateChangedEvent args)
        {
            if (_trackedEntity == null || args.Target != _trackedEntity)
                return;

            // TODO: whenever we get softcrit i want this to be updated to begin on softcrit instead
            if (args.NewMobState == MobState.Critical)
            {
                StartHeartbeat();
            }
            else
            {
                StopHeartbeat();
            }
        }

        private void StartHeartbeat()
        {
            if (_playing)
                return;

            _playing = true;
            _startTime = _timing.CurTime;
            _elapsed = TimeSpan.Zero;
            _currentInterval = _initialInterval;
            _nextTickTime = _startTime;
        }

        private void StopHeartbeat()
        {
            _playing = false;
            _elapsed = TimeSpan.Zero;
            _nextTickTime = TimeSpan.Zero;
            if (_playingAudio is { } pa)
            {
                _audio.Stop(pa.Entity);
                _playingAudio = null;
            }
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            if (!_playing || _trackedEntity == null)
                return;

            var now = _timing.CurTime;
            _elapsed = now - _startTime;

            // Prefer damage-based progress (how close to death).
            float progress;
            if (EntityManager.TryGetComponent(_trackedEntity, out DamageableComponent? damageable) &&
                _mobThresholdSystem.TryGetDeadPercentage(_trackedEntity.Value, damageable.TotalDamage, out var pct))
            {
                progress = MathF.Min(1f, MathF.Max(0f, pct.Value.Float()));
            }
            else
            {
                // time-based progress so we still accelerate if for some reason the entity doesnt have thresholds (HOW?)
                progress = MathF.Min(1f, MathF.Max(0f, (float)(_elapsed.TotalSeconds / _accelDuration.TotalSeconds)));
            }

            var intervalSeconds = _initialInterval.TotalSeconds - progress * (_initialInterval - _minInterval).TotalSeconds;
            _currentInterval = TimeSpan.FromSeconds(intervalSeconds);
            var pitch = _startPitch + progress * (_maxPitch - _startPitch);

            if (now >= _nextTickTime)
            {
                var audioParams = AudioParams.Default.WithPitchScale(pitch).WithVolume(-2f).WithLoop(false);
                var stream = _audio.PlayGlobal(_heartbeat, Filter.Local(), false, audioParams);
                if (stream != null)
                    _playingAudio = stream;

                _nextTickTime = now + _currentInterval;
            }

            // if entity is dead or otherwise not critical anymore, stop
            if (EntityManager.TryGetComponent(_trackedEntity, out MobStateComponent? mobState))
            {
                if (mobState.CurrentState != MobState.Critical)
                {
                    StopHeartbeat();
                }
            }
            else
            {
                StopHeartbeat();
            }
        }
    }
}
