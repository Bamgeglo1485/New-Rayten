using Content.Server.Chat.Managers;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Station.Systems;
using Content.Server.Spawners.Components;
using Content.Shared.Chat;
using Content.Shared.GameTicking.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Players;
using Robust.Server.Containers;
using Robust.Server.Player;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using Content.Server.Cloning;
using Content.Shared.Mind.Components;
using Content.Shared.Bed.Cryostorage;
using Robust.Server.Audio;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Damage;

namespace Content.Server.GameTicking.Rules;

/// <summary>
/// Мгновенно респавнит людей в крио
/// </summary>
public sealed partial class InstantRespawnRuleSystem : GameRuleSystem<InstantRespawnRuleComponent>
{
    [Dependency] private IChatManager _chatManager = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private StationSystem _station = default!;
    [Dependency] private CloningSystem _cloning = default!;
    [Dependency] private SharedMindSystem _mindSystem = default!;
    [Dependency] private SharedTransformSystem _transformSystem = default!;
    [Dependency] private ContainerSystem _container = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private DamageableSystem _damageable = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnMobStateChanged(MobStateChangedEvent args)
    {
        // Возрождаем критованных так как так круче
        if (args.NewMobState != MobState.Critical)
            return;

        if (!TryComp<ActorComponent>(args.Target, out var actor))
            return;

        // проверяем активен ли гейрул
        var query = QueryActiveRules();
        InstantRespawnRuleComponent? respawn = null;
        while (query.MoveNext(out _, out _, out var rule, out _))
        {
            respawn = rule;
            break;
        }

        if (respawn == null)
            return;

        var clone = RespawnPlayer((args.Target, actor));
        if (clone != null)
        {
            _audio.PlayPvs(respawn.RespawnSound, clone.Value);
            // Наносим урон для невозможности возродить и добивание чела в крите
            var damage = new DamageSpecifier();
            damage.DamageDict.Add("Cellular", 200);
            _damageable.TryChangeDamage(args.Target, damage);
        }
    }

    public EntityUid? RespawnPlayer(Entity<ActorComponent> player)
    {

        if (_station.GetStations().FirstOrNull() is not { } station)
            return null;

        if (!TryComp<MindContainerComponent>(player.Owner, out var mind) || mind.Mind == null)
            return null;

        var mindId = mind.Mind.Value;

        // Клонируем и переносим сознание
        _cloning.TryCloning(player, _transformSystem.GetMapCoordinates(player.Owner), "InstantRespawn", out var clone);
        if (clone == null)
            return null;

        _mindSystem.TransferTo(mindId, clone.Value, ghostCheckOverride: true);

        // Находим крио и помещаем его туда
        var query = EntityQueryEnumerator<ContainerSpawnPointComponent, ContainerManagerComponent, TransformComponent, CryostorageComponent>();
        var possibleContainers = new List<(EntityUid Uid, ContainerSpawnPointComponent SpawnPoint, ContainerManagerComponent Manager, TransformComponent Xform)>();

        while (query.MoveNext(out var uid, out var spawnPoint, out var container, out var xform, out var cryo))
        {
            if (_station.GetOwningStation(uid, xform) != station)
                continue;

            if (spawnPoint.SpawnType == SpawnPointType.Unset || spawnPoint.SpawnType == SpawnPointType.LateJoin)
                possibleContainers.Add((uid, spawnPoint, container, xform));
        }

        if (possibleContainers.Count > 0)
        {
            _random.Shuffle(possibleContainers);
            foreach (var (uid, spawnPoint, manager, xform) in possibleContainers)
            {
                if (!_container.TryGetContainer(uid, spawnPoint.ContainerId, out var container, manager))
                    continue;

                if (_container.Insert(clone.Value, container, containerXform: xform))
                    return clone.Value;
            }
        }

        return clone;
    }
}
