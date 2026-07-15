using Content.Shared.Mobs;
using Robust.Client.Player;
using Robust.Shared.Player;
using Robust.Shared.Audio;
using Robust.Client.Audio;
using Robust.Shared.Timing;

namespace Content.Client.Vanilla.Stinger;

public sealed class StingerSystem : EntitySystem
{
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;

    private readonly SoundSpecifier _death = new SoundPathSpecifier("/Audio/Vanilla/Effects/Stingers/deathStinger.ogg");
    private readonly SoundSpecifier _revive = new SoundPathSpecifier("/Audio/Vanilla/Effects/Stingers/reviveStinger.ogg");

    private TimeSpan _lastPlayTime = TimeSpan.Zero;
    private readonly TimeSpan _cooldown = TimeSpan.FromSeconds(10);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ActorComponent, MobStateChangedEvent>(OnMobStateChanged);
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
        else if (args.NewMobState == MobState.Critical)
            return;
        else if (args.NewMobState == MobState.Alive)
        {
            _audio.PlayGlobal(_revive, ent);
            _lastPlayTime = currentTime;
        }
    }
}
