using Content.Server.GameTicking;
using Content.Server.Mind;
using Content.Server.Station.Systems;
using Content.Server.Spawners.Components;
using Content.Server.Chat.Managers;
using Content.Shared.Chat;
using Content.Shared.Administration;
using Content.Shared.GameTicking;
using Content.Shared.CombatMode.Pacification;
using Content.Shared.Vanilla.CCVars;
using Content.Shared.Vanilla.Skill;
using Content.Shared.Vanilla.Background;
using Content.Shared.Mindshield.Components;
using Content.Shared.Clothing;
using Content.Shared.Damage;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.IdentityManagement;
using Content.Shared.Vanilla.TDM;
using Content.Shared.Ghost;
using Robust.Server.GameObjects;
using Robust.Shared.Utility;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.EntitySerialization;
using Robust.Shared.Map.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Map;
using Robust.Shared.Timing;
using Robust.Shared.Configuration;
using Timer = Robust.Shared.Timing.Timer;
using Robust.Shared.Player;
using Robust.Shared.Random;
using System.Linq;

namespace Content.Server.Vanilla.TDM;

public sealed class TDMSystem : EntitySystem
{
    record PlayerStats(string Name, int Kills, float Damage);
    [Dependency] private readonly MapSystem _mapSystem = default!;
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly MindSystem _mindSystem = default!;
    [Dependency] private readonly StationSpawningSystem _spawning = default!;
    [Dependency] protected readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedGhostSystem _ghosts = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    EntityUid? Currentrule = null;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TDMMarkerComponent, DamageChangedEvent>(OnDamageChanged, before: [typeof(MobThresholdSystem)]); //записываем урон который нанесли
        SubscribeLocalEvent<TDMMarkerComponent, DamageModifyEvent>(OnDamageModify);    //НО-френдлифаер
        SubscribeLocalEvent<TDMMarkerComponent, MobStateChangedEvent>(OnMobStateChanged); //Вычёркиваем

        SubscribeLocalEvent<TDMRuleComponent, MapInitEvent>(OnRuleInit);//новый геймрул кайф
        SubscribeLocalEvent<TDMRuleComponent, ComponentShutdown>(OnRuleShutDown); // это конец

        SubscribeNetworkEvent<TDMInfoRequest>(OnInfoRequest); //Пользователь запросил инфы
        SubscribeNetworkEvent<TPMeToTDMEvent>(OnArenaJoinRequest); //Пользователь захотел зайти на арену
        SubscribeLocalEvent<RoundEndTextAppendEvent>(OnRoundEnded);
        _playerManager.PlayerStatusChanged += OnPlayerStatusChanged;
    }
    public override void Shutdown()
    {
        base.Shutdown();

        _playerManager.PlayerStatusChanged -= OnPlayerStatusChanged;
    }
    private void OnRoundEnded(RoundEndTextAppendEvent ev)
    {
        foreach (var session in _playerManager.Sessions)
        {
            if (!session.AttachedEntity.HasValue) continue;

            var entityId = session.AttachedEntity.Value;
            EnsureComp<PacifiedComponent>(entityId);
        }

        if (Currentrule == null)
        {
            _gameTicker.RestartRound();
            return;
        }

        if (!TryComp<TDMRuleComponent>(Currentrule, out var rule))
        {
            _gameTicker.RestartRound();
            return;
        }

        GameOver(Currentrule.Value, rule);
        rule.LastRound = true;
    }

    private void OnArenaJoinRequest(TPMeToTDMEvent msg, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;

        if (session == null || Currentrule == null)
            return;

        if (!TryComp<TDMRuleComponent>(Currentrule, out var rule))
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

        if (session.AttachedEntity != null)
            _ghosts.SetCanReturnToBody(session.AttachedEntity.Value, false);
    }
    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs e)
    {
        if (e.NewStatus != SessionStatus.Disconnected || Currentrule == null || !TryComp<TDMRuleComponent>(Currentrule, out var rule))
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
        if (Currentrule != null)
        {
            if (!TryComp<TDMRuleComponent>(Currentrule, out var rule))
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

                bool odd = (rule.Playercount % 2 == 1) ? true : false;

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
                if (rule.TimeOnNewCycle <= TimeSpan.FromSeconds(19.5))
                {
                    return;
                }
                else
                {
                    //запускаем обратный отсчёт
                    var filter = Filter.Empty().AddPlayers(rule.Players);
                    _audio.PlayGlobal("/Audio/Vanilla/Effects/TDM/counting.ogg", filter, true);
                    rule.CurrentStatus = TDMStatus.unfreeze;
                }
            }

            //Размораживаем игроков и начинаем пвпшиться
            if (rule.CurrentStatus == TDMStatus.unfreeze && rule.TimeOnNewCycle >= TimeSpan.FromSeconds(30))
            {
                rule.Firstblooded = false;

                foreach (var player in rule.Players)
                {
                    if (!player.AttachedEntity.HasValue)
                        continue;

                    RemComp<AdminFrozenComponent>(player.AttachedEntity.Value);
                }
                rule.CurrentStatus = TDMStatus.started;
            }

            // Пора запустить новый цикл
            if (rule.CurrentStatus == TDMStatus.started && rule.TimeOnNewCycle >= rule.TimeToNewCycle)
            {
                rule.CurrentStatus = TDMStatus.ended;
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
    private void GameOver(EntityUid uid, TDMRuleComponent rule, bool notstartnewcycle = false)
    {
        var redguys = rule.PlayerCharacters.Count(p => p.Value == true);
        var blueguys = rule.PlayerCharacters.Count(p => p.Value == false);

        bool winner = blueguys > redguys ? false : true;

        List<PlayerStats> statsList = new();

        foreach (var (player, _) in rule.PlayerCharacters)
        {
            if (!TryComp<TDMMarkerComponent>(player, out var marker))
                continue;

            var name = MetaData(player).EntityName;
            int kills = marker.TotalKills;
            float damage = marker.TotalDamage.Float();

            statsList.Add(new PlayerStats(name, kills, damage));
        }

        var sorted = statsList.OrderByDescending(s => s.Kills).ToList();

        var result = "Игрок".PadRight(32) + "| " + "Убийств".PadRight(10) + "| " + "Урон".PadRight(10) + "\n";

        foreach (var stat in sorted)
        {
            string name = stat.Name.Length > 32 ? stat.Name.Substring(0, 32) : stat.Name;
            result += name.PadRight(32) + "| " +
                    stat.Kills.ToString().PadRight(10) + "| " +
                    ((int)stat.Damage).ToString().PadRight(10) + "\n";
        }
        Color draw = Color.Green;
        Color wincolor = winner ? Color.Red : Color.DodgerBlue;

        var message = Loc.GetString("tdm-gameover",
            ("winner", (blueguys == redguys) ? "other" : winner),
            ("result", result));

        DispatchMonospaceAnnouncement(
            Filter.Empty().AddPlayers(rule.Players),
            message,
            (blueguys == redguys) ? draw : wincolor);

        Timer.Spawn(TimeSpan.FromSeconds(1), () => QueueDel(rule.Arena)); //Удаляем прошлую арену

        if (rule.LastRound)
        {
            _gameTicker.RestartRound();
            return;
        }
        else
        {
            if (!notstartnewcycle)
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

        if (lastvalidSpawnerCoords == null)
            lastvalidSpawnerCoords = Transform(rule.Arena).Coordinates;

        var profile = _gameTicker.GetPlayerProfile(session);
        var mobUid = _spawning.SpawnPlayerMob(lastvalidSpawnerCoords.Value, null, profile, null);

        if (_mindSystem.TryGetMind(session.AttachedEntity!.Value, out var mindId, out var mindComp))
            _mindSystem.TransferTo(mindId, mobUid, true, mind: mindComp);

        //Замораживаем
        AddComp<AdminFrozenComponent>(mobUid);

        //Добавляем метку
        var marker = EnsureComp<TDMMarkerComponent>(mobUid);
        marker.Team = rule.NextTeam;
        marker.RuleLink = ruleEnt;
        //Добавляем предыстории
        var background = EnsureComp<AwaitBackgroundComponent>(mobUid);
        background.BackgroundGroup = (marker.Team) ? "RedGuyBackgroundGroup" : "BlueGuyBackgroundGroup";

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
        if ((args.NewMobState == MobState.Critical && args.OldMobState < args.NewMobState) || (args.NewMobState == MobState.Dead && args.OldMobState < MobState.Critical))
        {
            if (TryComp<DamageableComponent>(uid, out var damage))
            {
                _damageable.TryChangeDamage(uid, component.Damage, true, false, damage);
            }
            else return;
        }
        else return;

        if (component.RuleLink == null)
            return;

        if (!TryComp<TDMRuleComponent>(component.RuleLink, out var rulecomp))
            return;

        rulecomp.PlayerCharacters.Remove(uid);

        Color color = component.Team ? Color.DodgerBlue : Color.Red;

        if (args.Origin != null && TryComp<TDMMarkerComponent>(args.Origin, out var sourcecomp))
        {
            var origin = args.Origin.Value;

            if (origin != uid)
                sourcecomp.TotalKills++;

            if (TryComp<MetaDataComponent>(origin, out var originmeta) && TryComp<MetaDataComponent>(uid, out var entmeta))
            {
                string sourcename = originmeta.EntityName;
                string victimname = entmeta.EntityName;

                if (!rulecomp.Firstblooded)
                {
                    rulecomp.Firstblooded = true;

                    var filter = Filter.Empty().AddPlayers(rulecomp.Players);
                    _audio.PlayGlobal("/Audio/Vanilla/Effects/TDM/Firstblood.ogg", filter, true);
                    DispatchMonospaceAnnouncement(filter, Loc.GetString("tdm-firstblood", ("player", sourcename), ("victim", victimname)), color);
                }
                else
                {
                    var kills = sourcecomp.TotalKills;
                    var killSounds = new Dictionary<int, string>
                    {
                        { 2, "/Audio/Vanilla/Effects/TDM/Doublekill.ogg" },
                        { 3, "/Audio/Vanilla/Effects/TDM/TripleKill.ogg" },
                        { 4, "/Audio/Vanilla/Effects/TDM/UltraKill.ogg" },
                        { 5, "/Audio/Vanilla/Effects/TDM/Rampage.ogg" },
                    };

                    var filter = Filter.Empty().AddPlayers(rulecomp.Players);

                    if (killSounds.ContainsKey(kills))
                        _audio.PlayGlobal(killSounds[kills], filter, true);
                    DispatchMonospaceAnnouncement(Filter.Empty().AddPlayers(rulecomp.Players), Loc.GetString("tdm-killstreak", ("streak", kills), ("player", sourcename), ("victim", victimname)), color);
                }
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
        {
            Logger.Warning($"Не удалось загрузить арену по пути {rule.TDMProto.ArenaPath} на карту {rule.ArenaMapId.Value}");
            return null;
        }

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
    private void OnRuleInit(EntityUid uid, TDMRuleComponent rule, MapInitEvent args)
    {
        Currentrule = uid;
        var msg = new TDMInformation(rule.Playercount, rule.TimeForPlayersJoin, rule.CurrentStatus == TDMStatus.awaitstart);
        RaiseNetworkEvent(msg, Filter.Broadcast());
    }
    private void OnRuleShutDown(EntityUid uid, TDMRuleComponent component, ComponentShutdown args)
    {
        GameOver(uid, component, true);
        if (Currentrule == uid)
            Currentrule = null;
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


}
