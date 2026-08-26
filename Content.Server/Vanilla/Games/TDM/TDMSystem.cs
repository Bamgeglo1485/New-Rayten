using Content.Server.GameTicking;
using Content.Server.Mind;
using Content.Server.Station.Systems;
using Content.Server.Spawners.Components;
using Content.Server.Chat.Managers;
using Content.Shared.Chat;
using Content.Shared.Administration;
using Content.Shared.CombatMode.Pacification;
using Content.Shared.Vanilla.Background;
using Content.Shared.Damage.Systems;
using Content.Shared.Damage;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Vanilla.TDM;
using Content.Shared.Ghost;
using Content.Shared.Projectiles;
using Robust.Server.GameObjects;
using Robust.Shared.Utility;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.EntitySerialization;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Map;
using Robust.Shared.Timing;
using Timer = Robust.Shared.Timing.Timer;
using Robust.Shared.Player;
using Robust.Shared.Random;
using System.Linq;
using Robust.Shared.Physics.Events;
using Content.Shared.Vanilla.Games.TTT;
using Content.Shared.Body.Components;
using Content.Shared.Ghost.Systems;
using Content.Shared.Ghost.Components;

namespace Content.Server.Vanilla.TDM;

public sealed partial class TDMSystem : EntitySystem
{
    public sealed class PlayerStats(string name, int kills, float damage, int mmr)
    {
        public string Name { get; set; } = name;
        public int Kills { get; set; } = kills;
        public float Damage { get; set; } = damage;
        public int MMR { get; set; } = mmr;
    }

    [Dependency] private MapSystem _mapSystem = default!;
    [Dependency] private MapLoaderSystem _mapLoader = default!;
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private IGameTiming _gameTiming = default!;
    [Dependency] private MindSystem _mindSystem = default!;
    [Dependency] private StationSpawningSystem _spawning = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IChatManager _chatManager = default!;
    [Dependency] private SharedGhostSystem _ghosts = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private MetaDataSystem _metaSystem = default!;
    [Dependency] private MobThresholdSystem _thresh = default!;
    private EntityUid? _currentrule = null;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TDMMarkerComponent, DamageChangedEvent>(OnDamageChanged, before: [typeof(MobThresholdSystem)]); //записываем урон который нанесли
        SubscribeLocalEvent<TDMMarkerComponent, DamageModifyEvent>(OnDamageModify);
        SubscribeLocalEvent<TDMMarkerComponent, PreventCollideEvent>(OnPrventCollide);

        SubscribeLocalEvent<TDMMarkerComponent, MobStateChangedEvent>(OnMobStateChanged); //Вычёркиваем
        SubscribeLocalEvent<TDMMarkerComponent, MapInitEvent>(OnMarkerInit); //Вычёркиваем

        SubscribeLocalEvent<TDMRuleComponent, MapInitEvent>(OnRuleInit);//новый геймрул кайф
        SubscribeLocalEvent<TDMRuleComponent, ComponentShutdown>(OnRuleShutDown); // это конец

        SubscribeNetworkEvent<TDMInfoRequest>(OnInfoRequest); //Пользователь запросил инфы
        SubscribeNetworkEvent<TPMeToTDMEvent>(OnArenaJoinRequest); //Пользователь захотел зайти на арену
        SubscribeLocalEvent<RoundEndTextAppendEvent>(OnRoundEnded);
        _playerManager.PlayerStatusChanged += OnPlayerStatusChanged;
        LoadMMR();
    }
    public override void Shutdown()
    {
        base.Shutdown();
        _playerManager.PlayerStatusChanged -= OnPlayerStatusChanged;
        SaveMMR();
    }
    private void OnRoundEnded(RoundEndTextAppendEvent ev)
    {
        foreach (var session in _playerManager.Sessions)
        {
            if (!session.AttachedEntity.HasValue) continue;

            var entityId = session.AttachedEntity.Value;
            EnsureComp<PacifiedComponent>(entityId);
        }

        if (TryComp<TDMRuleComponent>(_currentrule, out var rule))
        {
            GameOver(_currentrule.Value, rule);
            rule.LastRound = true;
        }
    }

    private void OnArenaJoinRequest(TPMeToTDMEvent msg, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;

        if (session == null || _currentrule == null)
            return;

        if (!TryComp<TDMRuleComponent>(_currentrule, out var rule))
            return;

        if (rule.CurrentStatus != TDMStatus.awaitstart)
            return;

        if (rule.Players.Contains(session))
            return;

        rule.Playercount++;
        rule.Players.Add(session);
        //Сообщаем о том что добавился новый игрок
        var info = new TDMInformation(rule.Playercount, rule.TimeForPlayersJoin, rule.CurrentStatus == TDMStatus.awaitstart);
        RaiseNetworkEvent(info, Filter.Broadcast());

        if (session.AttachedEntity != null && TryComp<GhostComponent>(session.AttachedEntity, out var ghost))
            _ghosts.SetCanReturnToBody((session.AttachedEntity.Value, ghost), false);
    }
    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs e)
    {
        if (e.NewStatus != SessionStatus.Disconnected || _currentrule == null || !TryComp<TDMRuleComponent>(_currentrule, out var rule))
        {
            return;
        }

        if (!rule.Players.Contains(e.Session))
            return;

        rule.Players.Remove(e.Session);
        rule.Playercount--;

        //Сообщаем о том что игрок сдох
        var info = new TDMInformation(rule.Playercount, rule.TimeForPlayersJoin, rule.CurrentStatus == TDMStatus.awaitstart);
        RaiseNetworkEvent(info, Filter.Broadcast());
    }

    private void OnInfoRequest(TDMInfoRequest msg, EntitySessionEventArgs args)
    {
        if (_currentrule != null)
        {
            if (!TryComp<TDMRuleComponent>(_currentrule, out var rule))
                return;

            var response = new TDMInformation(rule.Playercount, rule.TimeForPlayersJoin, rule.CurrentStatus == TDMStatus.awaitstart);
            RaiseNetworkEvent(response, Filter.SinglePlayer(args.SenderSession));
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var currentTime = _gameTiming.CurTime;

        var query = EntityQueryEnumerator<TDMRuleComponent>();
        while (query.MoveNext(out var uid, out var rule))
        {
            if (currentTime < rule.NextUpdate)
                continue;

            rule.NextUpdate = currentTime + TimeSpan.FromSeconds(1);

            // ожидаем начала, собираем заявки
            if (rule.CurrentStatus == TDMStatus.awaitstart)
            {
                //Количество игроков меньше 2 не трогаем ваще
                if (rule.Playercount < 2)
                {
                    rule.TimeForPlayersJoin = TimeSpan.FromSeconds(30f);
                    return;
                }

                if (rule.TimeForPlayersJoin > TimeSpan.FromSeconds(0))
                {
                    rule.TimeForPlayersJoin -= TimeSpan.FromSeconds(1); //обратный отсчёт

                    return;
                }
                else
                {
                    rule.CurrentStatus = TDMStatus.startup; //время на сбор заявок вышло, спавним игроков

                }
            }

            //Начали раунд, спавним всех кто пожелал поучаствовать
            if (rule.CurrentStatus == TDMStatus.startup)
            {
                //Сообщаем о том что всё конец сбора заявок парни
                var msg = new TDMInformation(rule.Playercount, rule.TimeForPlayersJoin, rule.CurrentStatus == TDMStatus.awaitstart);
                RaiseNetworkEvent(msg, Filter.Broadcast());

                HashSet<EntityUid> usedspawners = new();

                //Желающих оказалось меньше чем два игрока, поэтому досрочно завершаем цикл
                if (rule.Playercount < 2)
                {
                    GameOver(uid, rule);
                    return;
                }

                var arena = SpawnArena(rule.Playercount, rule);

                if (arena == null)
                {
                    Log.Error("не удалось заспавнить арену для тдма");
                    return;
                }

                rule.Arena = arena.Value;

                var odd = rule.Playercount % 2 == 1;

                //проходим по всем игрокам и закидываем их на арену
                foreach (var player in rule.Players)
                {
                    if (!player.AttachedEntity.HasValue)
                        continue;

                    if (odd)
                    {
                        odd = false;
                        continue;
                    }

                    var spawner = AddPlayerToArena(player, uid, usedspawners);

                    if (spawner != null)
                        usedspawners.Add(spawner.Value);

                    //меняем команду для следующего игрока
                    rule.NextTeam = !rule.NextTeam;
                }
                rule.CurrentStatus = TDMStatus.countdown; //Вот теперь матч реально начался
            }

            rule.TimeOnNewCycle += TimeSpan.FromSeconds(1);

            //обратный отсчёт
            if (rule.CurrentStatus == TDMStatus.countdown)
            {
                if (rule.TimeOnNewCycle <= TimeSpan.FromSeconds(4.5))
                {
                    return;
                }
                else
                {
                    //запускаем обратный отсчёт
                    var filter = Filter.Empty().AddPlayers(rule.Players);
                    _audio.PlayGlobal(rule.CountDownSound, filter, true);
                    rule.CurrentStatus = TDMStatus.unfreeze;
                }
            }

            //Размораживаем игроков и начинаем пвпшиться
            if (rule.CurrentStatus == TDMStatus.unfreeze && rule.TimeOnNewCycle >= TimeSpan.FromSeconds(15))
            {
                rule.Firstblooded = false;

                foreach (var player in rule.Players)
                {
                    if (!player.AttachedEntity.HasValue)
                        continue;

                    RemComp<AdminFrozenComponent>(player.AttachedEntity.Value);
                    if (TryComp<BackgroundComponent>(player.AttachedEntity.Value, out var background)
                        && TryComp<NameOverlayComponent>(player.AttachedEntity.Value, out var markerName))
                    {

                        if (background.GeneralBackground == "BlueGuySpyBackground")
                            markerName.NameColor = Color.Red;
                        if (background.GeneralBackground == "RedGuySpyBackground")
                            markerName.NameColor = Color.DodgerBlue;
                        Dirty(player.AttachedEntity.Value, markerName);
                    }
                }
                rule.CurrentStatus = TDMStatus.started;
            }

            // Пора запустить новый цикл
            if (rule.CurrentStatus == TDMStatus.started && rule.TimeOnNewCycle >= rule.TimeToNewCycle)
            {
                GameOver(uid, rule);
            }
        }
    }

    //обновляем все значения
    public void NewCycle(EntityUid uid, TDMRuleComponent rule)
    {
        rule.Players = new(); //Сбрасываем предыдущих пользователей
        rule.PlayerCharacters = new(); //Сбрасываем предыдущих персонажей
        rule.Playercount = 0;
        rule.CurrentStatus = TDMStatus.awaitstart; //начинаем собирать игроков в раунд
        rule.TimeForPlayersJoin = TimeSpan.FromSeconds(30f);
        rule.TimeOnNewCycle = TimeSpan.FromSeconds(0);
        //Сообщаем о том что начался новый цикл
        var msg = new TDMInformation(rule.Playercount, rule.TimeForPlayersJoin, rule.CurrentStatus == TDMStatus.awaitstart);
        RaiseNetworkEvent(msg, Filter.Broadcast());
    }

    /// <summary>
    /// Завершает пизделку, сообщает кто победил, запускает новый цикл, если это не ласт раунд
    /// </summary>
    private void GameOver(EntityUid uid, TDMRuleComponent rule)
    {
        rule.CurrentStatus = TDMStatus.ended;

        var redguys = rule.PlayerCharacters.Count(p => p.Value == true);
        var blueguys = rule.PlayerCharacters.Count(p => p.Value == false);

        var winner = blueguys > redguys ? false : true;
        List<PlayerStats> statsList = new();
        var statsByEntity = new Dictionary<EntityUid, PlayerStats>();

        var query = EntityQueryEnumerator<TDMMarkerComponent>();

        while (query.MoveNext(out var player, out var marker))
        {
            if (!TryComp<TDMRuleComponent>(marker.RuleLink, out var rulelink))
                continue;

            if (rulelink != rule)
                continue;
            if (rule.LastRound)
                AddMMR(marker.UserId, marker.Team == winner ? +25 : -25);

            var target = marker.Summoner ?? player;

            if (!statsByEntity.TryGetValue(target, out var stats))
            {
                var name = MetaData(target).EntityName;
                stats = new PlayerStats(name, 0, 0f, GetMMR(marker.UserId));
                statsByEntity[target] = stats;
            }

            stats.Kills += marker.TotalKills;
            stats.Damage += marker.TotalDamage.Float();
        }

        statsList.AddRange(statsByEntity.Values);

        var sorted = statsList
            .OrderByDescending(s => s.Kills)
            .ThenByDescending(s => s.Damage)
            .ThenByDescending(s => s.MMR)
            .ToList();

        var result = $"{"Игрок".PadRight(16)}| {"Убийств".PadRight(7)}| {"Урон".PadRight(6)}| {"MMR".PadRight(5)} \n";

        foreach (var stat in sorted)
        {
            var name = stat.Name.Length > 16 ? stat.Name[..16] : stat.Name;
            result += name.PadRight(16) + "| " + stat.Kills.ToString().PadRight(7) + "| " + ((int)stat.Damage).ToString().PadRight(6) + "| " + (stat.MMR).ToString().PadRight(5) + "\n";
        }

        var draw = Color.Green;
        var wincolor = winner ? Color.Red : Color.DodgerBlue;

        var message = Loc.GetString("tdm-gameover",
            ("winner", (blueguys == redguys) ? "other" : winner),
            ("result", result));

        DispatchMonospaceAnnouncement(
            Filter.Empty().AddPlayers(rule.Players),
            message,
            (blueguys == redguys) ? draw : wincolor);


        Timer.Spawn(TimeSpan.FromSeconds(3), () => QueueDel(rule.Arena)); //Удаляем прошлую арену
        if (rule.LastRound)
        {
            SaveMMR();
            _gameTicker.RestartRound();//рестартим раунд
        }
        else
        {
            NewCycle(uid, rule);
        }
    }

    //Метод добавляет игрока на арену, возвращает спавн, на котором игрок был заспавнен
    public EntityUid? AddPlayerToArena(ICommonSession session, EntityUid ruleEnt, HashSet<EntityUid> usedspawners)
    {
        if (!TryComp<TDMRuleComponent>(ruleEnt, out var rule))
            return null;

        //Спавним игрока на арене
        var targetSpawnId = rule.NextTeam ? "SpawnPointTeamRed" : "SpawnPointTeamBlue";

        EntityUid? usedspawner = null;
        EntityCoordinates? lastvalidSpawnerCoords = null;

        var query = EntityQueryEnumerator<SpawnPointComponent, MetaDataComponent, TransformComponent>();
        while (query.MoveNext(out var spawnpoint, out _, out var meta, out var trans))
        {
            if (meta.EntityPrototype?.ID != targetSpawnId)
                continue;

            if (trans.GridUid != rule.Arena)
                continue;

            if (usedspawners.Contains(spawnpoint))
                continue;

            usedspawner = spawnpoint;
            lastvalidSpawnerCoords = trans.Coordinates;
            break;
        }

        lastvalidSpawnerCoords ??= Transform(rule.Arena).Coordinates;

        var profile = _gameTicker.GetPlayerProfile(session);
        var mobUid = _spawning.SpawnPlayerMob(lastvalidSpawnerCoords.Value, null, profile, null);

        if (_mindSystem.TryGetMind(session.AttachedEntity!.Value, out var mindId, out var mindComp))
            _mindSystem.TransferTo(mindId, mobUid, true, mind: mindComp);

        //Добавляем метку
        var marker = EnsureComp<TDMMarkerComponent>(mobUid);
        marker.Team = rule.NextTeam;
        marker.RuleLink = ruleEnt;
        marker.UserId = session.UserId;
        var nameMarker = EnsureComp<NameOverlayComponent>(mobUid);
        nameMarker.Name = session.Name;
        nameMarker.NameColor = marker.Team ? Color.Red : Color.DodgerBlue;
        _metaSystem.SetEntityName(mobUid, session.Name);
        //Добавляем предыстории
        var background = EnsureComp<AwaitBackgroundComponent>(mobUid);
        background.BackgroundGroup = marker.Team ? "RedGuyBackgroundGroup" : "BlueGuyBackgroundGroup";
        //трешхолд смерти
        _thresh.SetMobStateThreshold(mobUid, 100f, MobState.Dead);
        //Замораживаем
        EnsureComp<AdminFrozenComponent>(mobUid);
        //bloodstream
        if (TryComp<BloodstreamComponent>(mobUid, out var blood))
            blood.MaxBleedAmount = 0;

        rule.PlayerCharacters[mobUid] = marker.Team; //Добавляем в список игроков
        return usedspawner;
    }

    private void OnDamageChanged(EntityUid uid, TDMMarkerComponent component, DamageChangedEvent args)
    {
        if (!args.DamageIncreased || args.DamageDelta == null)
            return;

        TryComp<TDMMarkerComponent>(args.Origin, out var sourcecomp);
        if (args.Origin != null && args.Origin.Value != uid && sourcecomp != null)
            sourcecomp.TotalDamage += args.DamageDelta.GetTotal();
    }

    private void OnMobStateChanged(EntityUid uid, TDMMarkerComponent component, MobStateChangedEvent args)
    {
        if (args.OldMobState != MobState.Alive)
            return;

        if (component.RuleLink == null)
            return;

        if (!TryComp<TDMRuleComponent>(component.RuleLink, out var rulecomp))
            return;

        rulecomp.PlayerCharacters.Remove(uid);

        if (TryComp<TDMMarkerComponent>(args.Origin, out var sourcecomp))
        {
            if (args.Origin == null)
                return;
            var origin = args.Origin.Value;
            var nameMarker = EnsureComp<NameOverlayComponent>(origin);
            var color = nameMarker.NameColor;

            if (origin != uid)
                sourcecomp.TotalKills++;

            var source = sourcecomp.Summoner ?? args.Origin.Value;
            var filter = Filter.Empty().AddPlayers(rulecomp.Players);
            var sourcename = MetaData(source).EntityName;
            var victimname = MetaData(uid).EntityName;

            if (!rulecomp.Firstblooded)
            {
                rulecomp.Firstblooded = true;
                _audio.PlayGlobal(rulecomp.FirstBloodSound, filter, true);
                DispatchMonospaceAnnouncement(filter, Loc.GetString("tdm-firstblood", ("player", sourcename), ("victim", victimname)), color);
            }
            else
            {
                var kills = sourcecomp.TotalKills;
                if (kills > 1)
                {
                    var sound = rulecomp.KillSounds.GetValueOrDefault(kills) ?? rulecomp.KillSounds[5];
                    _audio.PlayGlobal(sound, filter, true);
                }
                DispatchMonospaceAnnouncement(filter, Loc.GetString("tdm-killstreak", ("streak", kills), ("player", sourcename), ("victim", victimname)), color);
            }
        }

        var redguys = rulecomp.PlayerCharacters.Count(p => p.Value == true);
        var blueguys = rulecomp.PlayerCharacters.Count(p => p.Value == false);

        if (blueguys <= 0 || redguys <= 0)
            GameOver(component.RuleLink.Value, rulecomp);
    }

    private EntityUid? SpawnArena(int playerCount, TDMRuleComponent rule)
    {
        rule.TDMProto = PickRandomArena(playerCount);

        if (rule.TDMProto == null)
        {
            Log.Error($"Не удалось найти никакой подходящей карты");
            return null;
        }

        // Проверка: если карта не существует, создаём новую
        if (rule.ArenaMapId == null || !_mapSystem.MapExists(rule.ArenaMapId))
        {
            _mapSystem.CreateMap(out var newMapId);
            rule.ArenaMapId = newMapId;
        }

        var opts = DeserializationOptions.Default;

        // Пытаемся загрузить грид на карту
        if (!_mapLoader.TryLoadGrid(rule.ArenaMapId.Value, new ResPath(rule.TDMProto.ArenaPath), out var grid, opts))
            return null;

        return grid;
    }
    private TDMMapPrototype? PickRandomArena(int playerCount)
    {
        var validPrototypes = new List<TDMMapPrototype>();

        foreach (var proto in _prototypeManager.EnumeratePrototypes<TDMMapPrototype>())
        {
            if (proto.ArenaParty >= playerCount)
                validPrototypes.Add(proto);
        }

        if (validPrototypes.Count == 0)
            return null;

        return _random.Pick(validPrototypes);
    }

    private void OnMarkerInit(EntityUid uid, TDMMarkerComponent marker, MapInitEvent args)
    {
        marker.RuleLink = _currentrule;
        var transform = Transform(uid);
        var entitiesInRange = _lookup.GetEntitiesInRange(transform.Coordinates, 2f);
        foreach (var entity in entitiesInRange)
        {
            if (HasComp<TDMSummonerComponent>(entity))
            {
                marker.Summoner = entity;
                return;
            }
        }
    }
    private void OnRuleInit(EntityUid uid, TDMRuleComponent rule, MapInitEvent args)
    {
        _currentrule = uid;
        var msg = new TDMInformation(rule.Playercount, rule.TimeForPlayersJoin, rule.CurrentStatus == TDMStatus.awaitstart);
        RaiseNetworkEvent(msg, Filter.Broadcast());
    }
    private void OnRuleShutDown(EntityUid uid, TDMRuleComponent component, ComponentShutdown args)
    {
        QueueDel(component.Arena);
        if (_currentrule == uid)
            _currentrule = null;
    }
    public void DispatchMonospaceAnnouncement(Filter filter, string rawMessage, Color color)
    {
        var formatted = "[font=\"Monospace\"]" + rawMessage + "[/font]";
        _chatManager.ChatMessageToManyFiltered(
            filter,
            ChatChannel.Radio,
            rawMessage,
            formatted,
            EntityUid.Invalid,
            hideChat: false,
            recordReplay: true,
            colorOverride: color
        );
    }
    //НО-френдлифаер
    private void OnDamageModify(EntityUid uid, TDMMarkerComponent component, DamageModifyEvent args)
    {
        if (!TryComp<TDMMarkerComponent>(args.Origin, out var sourcecomp))
            return;

        if (component.Team != sourcecomp.Team)
            return;

        // Полностью обнуляем урон
        args.Damage = new DamageSpecifier();
    }
    private void OnPrventCollide(EntityUid uid, TDMMarkerComponent component, ref PreventCollideEvent args)
    {
        if (args.Cancelled)
            return;

        if (!TryComp<ProjectileComponent>(args.OtherEntity, out var projectileComp))
            return;

        if (!TryComp<TDMMarkerComponent>(projectileComp.Shooter, out var otherMarker))
            return;

        if (otherMarker.Team == component.Team)
            args.Cancelled = true;
    }
}
