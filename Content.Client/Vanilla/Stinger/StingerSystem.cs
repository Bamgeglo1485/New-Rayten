using Content.Shared.Mobs;
using Content.Shared.Medical;
using Robust.Client.Player;
using Robust.Shared.Player;
using Robust.Shared.Audio;
using Robust.Client.Audio;
using Robust.Shared.Timing;

namespace Content.Client.Vanilla.Stinger;

public sealed partial class StingerSystem : EntitySystem
{
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IGameTiming _gameTiming = default!;

    private readonly SoundSpecifier _death = new SoundPathSpecifier("/Audio/Vanilla/Effects/Stingers/deathStinger.ogg");
    private readonly SoundSpecifier _revive = new SoundPathSpecifier("/Audio/Vanilla/Effects/Stingers/reviveStinger.ogg");

    private TimeSpan _lastPlayTime = TimeSpan.Zero;
    private readonly TimeSpan _cooldown = TimeSpan.FromSeconds(10);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ActorComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<TargetBeforeDefibrillatorZapsEvent>(OnDefibZap);
    }

    private void OnMobStateChanged(Entity<ActorComponent> ent, ref MobStateChangedEvent args)
    {
        if (_player.LocalSession?.AttachedEntity != args.Target)
            return;

        var currentTime = _gameTiming.CurTime;
        if (currentTime - _lastPlayTime < _cooldown)
            return;

        if (args.NewMobState == MobState.Dead)
        {
            _audio.PlayGlobal(_death, ent);
            _lastPlayTime = currentTime;
        }
    }

    private void OnDefibZap(TargetBeforeDefibrillatorZapsEvent args)
    {
        if (_player.LocalSession?.AttachedEntity != args.DefibTarget)
            return;

        _audio.PlayGlobal(_revive, args.DefibTarget);
    }
}
