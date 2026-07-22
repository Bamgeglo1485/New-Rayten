using Content.Server.Chat.Managers;
using Content.Server.Station.Systems;
using Content.Server.Cloning;
using Content.Server.Chat.Systems;
using Content.Server.Radio.EntitySystems;
using Content.Server.StationRecords.Systems;
using Content.Shared.Chat;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Vanilla.Entities.RespawnMachine;
using Content.Shared.Mind.Components;
using Content.Shared.StationRecords;
using Content.Shared.Forensics.Components;
using Content.Shared.GameTicking;
using Robust.Server.Audio;
using Robust.Server.Containers;
using Robust.Server.Player;
using Robust.Shared.Containers;
using Robust.Shared.Timing;
using Robust.Shared.Player;
using Robust.Shared.Random;
using System.Linq;
using Content.Shared.DeviceLinking;
using Content.Shared.DeviceLinking.Events;
using Robust.Shared.Prototypes;
using System.Numerics;
using Content.Shared.Inventory;
using Content.Shared.Objectives.Systems;

namespace Content.Server.Vanilla.Entities.RespawnMachine;

public sealed partial class RespawnMachineSystem : EntitySystem
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
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private RadioSystem _radio = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private StationRecordsSystem _recordsSystem = default!;
    [Dependency] private SharedDeviceLinkSystem _deviceLink = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private TargetSystem _target = default!;

    private readonly List<RespawnQueueEntry> _respawnQueue = new();
    private readonly Dictionary<string, int> _respawnCounts = new();
    private const int MaxRespawns = 3;
    public string _bluespaceEffectPrototype { get; set; } = "EffectFlashBluespace";

    private const float UpdateInterval = 5f;
    private float _updateTimer = 0f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);

        SubscribeLocalEvent<RespawnMachineComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<RespawnMachineComponent, NewLinkEvent>(OnNewLink);
        SubscribeLocalEvent<RespawnMachineComponent, LinkAttemptEvent>(OnLinkAttempt);
        SubscribeLocalEvent<RespawnMachineComponent, PortDisconnectedEvent>(OnPortDisconnected);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _respawnQueue.Clear();
        _respawnCounts.Clear();
        _updateTimer = 0f;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_respawnQueue.Count == 0)
            return;

        var currentTime = _timing.CurTime;

        _updateTimer += frameTime;

        if (_updateTimer < UpdateInterval)
            return;

        _updateTimer = 0f;

        for (int i = _respawnQueue.Count - 1; i >= 0; i--)
        {
            var entry = _respawnQueue[i];

            if (currentTime < entry.RespawnTime)
                continue;

            if (!_mobState.IsDead(entry.Player))
            {
                _respawnQueue.RemoveAt(i);
                continue;
            }

            if (!EntityManager.EntityExists(entry.Player))
            {
                _respawnQueue.RemoveAt(i);
                continue;
            }

            if (RespawnPlayer(entry))
            {
                _respawnQueue.RemoveAt(i);
            }
        }
    }

    private void OnMobStateChanged(MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        if (!TryComp<ActorComponent>(args.Target, out var actor))
            return;

        if (_respawnQueue.Any(e => e.Player == args.Target))
            return;

        if (!TryComp<MindContainerComponent>(args.Target, out var mind) || mind.Mind == null)
            return;

        if (!TryComp<MindComponent>(mind.Mind, out var mindComp))
            return;

        if (!_playerManager.TryGetSessionById(mindComp.UserId, out var session))
            return;

        bool forceRespawn = HasComp<ForceRespawnComponent>(args.Target);

        // проверяем ДНК и лимит
        if (!CanRespawn(args.Target, out var dna, out var currentRespawns) && !forceRespawn)
        {
            _chatManager.DispatchServerMessage(session,
                 $"Вы исчерпали лимит возрождений ({MaxRespawns}). При гниении ваша смерть окончательна!");
            return;
        }

        var stationUid = _station.GetOwningStation(args.Target);
        if (stationUid == null)
            return;

        if (!LegitToRespawn(stationUid.Value, args.Target))
            return;

        // находим доступную машину
        var machine = FindMachine(stationUid.Value);
        if (machine == null)
            return;

        // добавляем игрока в очередь
        var entry = new RespawnQueueEntry
        {
            Player = args.Target,
            Machine = machine.Value,
            RespawnTime = _timing.CurTime + TimeSpan.FromSeconds(machine.Value.Comp.RespawnDelay),
            Actor = actor,
            Dna = dna
        };

        _respawnQueue.Add(entry);

        if (!forceRespawn)
        {
            _chatManager.DispatchServerMessage(session,
                $"Вы будете воскрешены через {machine.Value.Comp.RespawnDelay / 60} минут. Осталось возрождений: {MaxRespawns - currentRespawns} из {MaxRespawns}.");
        }
        else
        {
            _chatManager.DispatchServerMessage(session,
                $"Вы будете воскрешены через {machine.Value.Comp.RespawnDelay / 60} минут.");
        }
    }

    private bool CanRespawn(EntityUid player, out string? dna, out int currentRespawns)
    {
        dna = null;
        currentRespawns = 0;

        if (!TryComp<DnaComponent>(player, out var dnaComp))
            return false;

        dna = dnaComp.DNA;
        if (string.IsNullOrEmpty(dna))
            return false;

        currentRespawns = _respawnCounts.GetValueOrDefault(dna, 0);

        return currentRespawns < MaxRespawns;
    }

    private bool LegitToRespawn(EntityUid stationUid, EntityUid player)
    {
        if (HasComp<ForceRespawnComponent>(player))
            return true;

        var playerName = MetaData(player).EntityName;
        if (string.IsNullOrEmpty(playerName))
            return false;

        var records = _recordsSystem.GetRecordsOfType<GeneralStationRecord>(stationUid);
        foreach (var record in records)
        {
            if (record.Item2.Name.Equals(playerName, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private Entity<RespawnMachineComponent>? FindMachine(EntityUid stationUid)
    {
        var query = EntityQueryEnumerator<RespawnMachineComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out var respawn, out var xform))
        {
            if (_station.GetOwningStation(uid, xform) != stationUid)
                continue;

            return (uid, respawn);
        }

        return null;
    }

    private bool RespawnPlayer(RespawnQueueEntry entry)
    {
        var player = entry.Player;
        var machine = entry.Machine;
        var dna = entry.Dna;

        if (!TryComp<MindContainerComponent>(player, out var mind) || mind.Mind == null)
            return false;

        var mindId = mind.Mind.Value;

        // получаем контейнер машины
        if (!TryComp<ContainerManagerComponent>(machine, out var manager))
            return false;

        if (!_container.TryGetContainer(machine, "storage", out var container, manager))
            return false;

        var body_to_clone = player;
        if (_random.Prob(machine.Comp.WrongBodyChance))
        {
            var allHumans = _target.GetAliveHumans(player);
            if (allHumans.Count > 0)
                body_to_clone = _random.Pick(allHumans);
        }

        // клонируем
        var coordinates = _transformSystem.GetMapCoordinates(player);
        if (!_cloning.TryCloning(body_to_clone, coordinates, "CloningPod", out var clone) || clone == null)
            return false;

        Spawn(_bluespaceEffectPrototype, Transform(player).Coordinates);

        // увеличиваем счетчик
        if (!string.IsNullOrEmpty(dna))
        {
            if (_respawnCounts.ContainsKey(dna))
                _respawnCounts[dna]++;
            else
                _respawnCounts[dna] = 1;
        }

        _mindSystem.TransferTo(mindId, clone, ghostCheckOverride: true);

        // перемещаем в машинуs
        _container.EmptyContainer(container, true, Transform(machine).Coordinates);
        _container.Insert(clone.Value, container);

        if (machine.Comp.InventoryTaker != null)
            TeleportCorpseItemsToInventoryTaker(player, machine.Comp.InventoryTaker.Value);

        if (machine.Comp.RespawnSound != null)
            _audio.PlayPvs(_audio.ResolveSound(machine.Comp.RespawnSound), clone.Value);

        var message = $"Сотрудник {MetaData(clone.Value).EntityName} воскрешён!";
        _chat.TrySendInGameICMessage(machine, message, InGameICChatType.Speak, true);
        _radio.SendRadioMessage(machine, message, "Common", machine);

        return true;
    }

    private void TeleportCorpseItemsToInventoryTaker(EntityUid corpse, EntityUid inventoryTaker)
    {
        if (!TryComp<InventoryComponent>(corpse, out var inventoryComp))
            return;

        var targetCoords = Transform(inventoryTaker).Coordinates;
        var itemsToTeleport = new List<EntityUid>();

        // сначала обрабатываем карманы и кпк так как при снятии комбеза они падают
        var prioritySlots = new List<string> { "id", "pocket1", "pocket2" };

        foreach (var slotName in prioritySlots)
        {
            if (!_inventory.TryGetSlotEntity(corpse, slotName, out var item) || item == null)
                continue;

            var itemEntity = item.Value;
            if (Deleted(itemEntity) || Terminating(itemEntity))
                continue;

            if (_inventory.TryUnequip(corpse, slotName, true, true))
            {
                itemsToTeleport.Add(itemEntity);
            }
        }

        var otherSlots = inventoryComp.Slots;
        // теперь остальные слоты
        foreach (var slot in otherSlots)
        {
            if (!_inventory.TryGetSlotEntity(corpse, slot.Name, out var item) || item == null)
                continue;

            var itemEntity = item.Value;
            if (Deleted(itemEntity) || Terminating(itemEntity))
                continue;

            if (_inventory.TryUnequip(corpse, slot.Name, true, true))
            {
                itemsToTeleport.Add(itemEntity);
            }
        }

        // телепортируем все вещи
        foreach (var item in itemsToTeleport)
        {
            if (Deleted(item) || Terminating(item) || !EntityManager.EntityExists(item))
                continue;

            _transformSystem.SetCoordinates(item, targetCoords);

            var offset = new Vector2(
                _random.NextFloat(-0.3f, 0.3f),
                _random.NextFloat(-0.3f, 0.3f));
            _transformSystem.SetLocalPosition(item, Transform(item).LocalPosition + offset);
        }
    }

    private void OnMapInit(Entity<RespawnMachineComponent> ent, ref MapInitEvent args)
    {
        if (!TryComp<DeviceLinkSourceComponent>(ent, out var source))
            return;

        var linkedEntities = _deviceLink.GetLinkedSinks((ent.Owner, source), ent.Comp.LinkingPort);

        foreach (var sink in linkedEntities)
        {
            if (!HasComp<InventoryTakerComponent>(sink))
                continue;

            ent.Comp.InventoryTaker = sink;
            break;
        }
    }

    private void OnNewLink(Entity<RespawnMachineComponent> ent, ref NewLinkEvent args)
    {
        if (args.SourcePort != ent.Comp.LinkingPort || !HasComp<InventoryTakerComponent>(args.Sink))
            return;

        ent.Comp.InventoryTaker = args.Sink;
    }

    private void OnLinkAttempt(Entity<RespawnMachineComponent> ent, ref LinkAttemptEvent args)
    {
        if (ent.Comp.InventoryTaker != null)
            args.Cancel();
    }

    private void OnPortDisconnected(Entity<RespawnMachineComponent> ent, ref PortDisconnectedEvent args)
    {
        if (args.Port != ent.Comp.LinkingPort || ent.Comp.InventoryTaker == null)
            return;

        ent.Comp.InventoryTaker = null;
    }


    private sealed class RespawnQueueEntry
    {
        public EntityUid Player { get; set; }
        public Entity<RespawnMachineComponent> Machine { get; set; }
        public TimeSpan RespawnTime { get; set; }
        public ActorComponent Actor { get; set; } = default!;
        public string? Dna { get; set; }
    }
}
