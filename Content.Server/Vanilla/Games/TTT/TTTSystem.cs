using Content.Server.GameTicking;
using Content.Server.Mind;
using Content.Server.Station.Systems;
using Content.Server.Spawners.Components;
using Content.Server.Chat.Managers;
using Content.Server.Respawn;
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
using Content.Shared.Ghost;
using Content.Shared.Roles;
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

using Content.Shared.Vanilla.TDM;
using Content.Shared.Vanilla.Games.TTT;

namespace Content.Server.Vanilla.Games.TTT;

public sealed class TTTSystem : EntitySystem
{
    private static readonly string[] GUNS = new[] 
    { 
        "Musket", "WeaponPistolFlintlock", "WeaponLaserSvalinn", "WeaponDominator", "WeaponEnergyShotgun", "WeaponAssaultDominator", "MakeshiftShield",
        "WeaponMakeshiftLaser", "WeaponLaserCarbinePractice", "WeaponLaserCarbine", 
        "WeaponLaserCannon", "WeaponPistolViper", "WeaponPistolCobra", "WeaponPistolMk58", "WeaponPistolN1984",
        "WeaponRevolverDeckard","WeaponRevolverInspector","WeaponRevolverMateba","WeaponRevolverPython","WeaponRevolverPirate","WeaponRifleLecter","WeaponRifleEstoc","WeaponRifleFoam","WeaponShotgunDoubleBarreled",
        "WeaponShotgunKammerer","WeaponShotgunSawn","WeaponShotgunHandmade","WeaponShotgunBlunderbuss","WeaponShotgunImprovised","WeaponSubMachineGunC20r","WeaponSubMachineGunDrozd","WeaponSubMachineGunWt550","WeaponImprovisedPneumaticCannon"
    };        
    private Dictionary<ICommonSession, int> KARMA = new();
    
    private sealed class PlayerStats
    {
        public string Name { get; set; }
        public int Kills { get; set; }
        public int Karma { get; set; }
        public TTTRole Role { get; set; }

        public PlayerStats(string name, int kills, int karma, TTTRole role)
        {
            Name = name;
            Kills = kills;
            Karma = karma;
            Role = role;
        }
    }

    [Dependency] private readonly MapSystem _mapSystem = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
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
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SpecialRespawnSystem _specialRespawn = default!;
    [Dependency] private readonly LoadoutSystem _loadout = default!;
    
    EntityUid? Currentrule = null;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TTTMarkerComponent, DamageModifyEvent>(OnDamageModify);    //Туду урон в зависимости от крамы
        SubscribeLocalEvent<TTTMarkerComponent, MobStateChangedEvent>(OnMobStateChanged); //Вычёркиваем
        SubscribeLocalEvent<TTTMarkerComponent, MapInitEvent>(OnMarkerInit);

        SubscribeLocalEvent<TTTRuleComponent, MapInitEvent>(OnRuleInit);//новый геймрул кайф
        SubscribeLocalEvent<TTTRuleComponent, ComponentShutdown>(OnRuleShutDown); // это конец

        SubscribeNetworkEvent<TTTInfoRequest>(OnInfoRequest); //Пользователь запросил инфы
        SubscribeNetworkEvent<TPMeToTTTEvent>(OnTTTJoinRequest); //Пользователь захотел зайти на арену

        _playerManager.PlayerStatusChanged += OnPlayerStatusChanged;
    }
    public override void Shutdown()
    {
        base.Shutdown();
        _playerManager.PlayerStatusChanged -= OnPlayerStatusChanged;
    }

    private void OnTTTJoinRequest(TPMeToTTTEvent msg, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;

        if (session == null || Currentrule == null)
            return;

        if (!TryComp<TTTRuleComponent>(Currentrule, out var rule))
            return;

        if (rule.CurrentStatus != TTTStatus.awaitstart)
            return;

        if (!KARMA.ContainsKey(session))
        {
            KARMA[session] = 1000;
        }
        else if (KARMA[session]<=0)
        {
            return;
        }


        if (rule.Players.Contains(session))
            return;

        rule.Playercount++;
        rule.Players.Add(session);
        //Сообщаем о том что добавился новый игрок
        var info = new TTTInformation(rule.Playercount, rule.TimeForPlayersJoin, rule.CurrentStatus == TTTStatus.awaitstart);
        RaiseNetworkEvent(info, Filter.Broadcast());
        if (session.AttachedEntity != null)
            _ghosts.SetCanReturnToBody(session.AttachedEntity.Value, false);
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs e)
    {
        if (e.NewStatus != SessionStatus.Disconnected || Currentrule == null || !TryComp<TTTRuleComponent>(Currentrule, out var rule))
        {
            return;
        }

        if (!rule.Players.Contains(e.Session))
            return;

        rule.Players.Remove(e.Session);
        rule.Playercount--;

        //Сообщаем о том что игрок ливнул с позором
        var info = new TTTInformation(rule.Playercount, rule.TimeForPlayersJoin, rule.CurrentStatus == TTTStatus.awaitstart);
        RaiseNetworkEvent(info, Filter.Broadcast());
    }

    private void OnInfoRequest(TTTInfoRequest msg, EntitySessionEventArgs args)
    {
        if (Currentrule != null)
        {
            if (!TryComp<TTTRuleComponent>(Currentrule, out var rule))
                return;

            var response = new TTTInformation(rule.Playercount, rule.TimeForPlayersJoin, rule.CurrentStatus == TTTStatus.awaitstart);
            RaiseNetworkEvent(response, Filter.SinglePlayer(args.SenderSession));
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var currentTime = _gameTiming.CurTime;

        var query = EntityQueryEnumerator<TTTRuleComponent>();
        while (query.MoveNext(out var uid, out var rule))
        {
            if (currentTime < rule.NextUpdate)
                continue;

            rule.NextUpdate = currentTime + TimeSpan.FromSeconds(1);

            // ожидаем начала, собираем заявки
            if (rule.CurrentStatus == TTTStatus.awaitstart)
            {
                //Количество игроков меньше 4 не трогаем ваще
                if (rule.Playercount < 4)
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
                    rule.CurrentStatus = TTTStatus.startup; //время на сбор заявок вышло, спавним игроков
                }
            }

            //Начали раунд, спавним всех кто пожелал поучаствовать
            if (rule.CurrentStatus == TTTStatus.startup)
            {
                //Сообщаем о том что всё конец сбора заявок парни
                var msg = new TTTInformation(rule.Playercount, rule.TimeForPlayersJoin, rule.CurrentStatus == TTTStatus.awaitstart);
                RaiseNetworkEvent(msg, Filter.Broadcast());

                //Желающих оказалось меньше чем 4 игрока, поэтому досрочно завершаем цикл
                if (rule.Playercount < 4)
                {
                    GameOver(uid, rule);
                    return;
                }

                var arena = SpawnArena(rule.Playercount, rule);

                if (arena == null)
                {
                    Log.Error("не удалось заспавнить арену для тттма");
                    return;
                }

                rule.Arena = arena.Value;
                //проходим по всем игрокам и закидываем их на арену
                foreach (var player in rule.Players)
                {
                    if (!player.AttachedEntity.HasValue)
                        continue;

                    AddPlayerToArena(player, uid);
                    var message = Loc.GetString("ttt-awaitrole-brief");
                    var wrappedMessage = Loc.GetString("chat-manager-server-wrap-message", ("message", message));
                    _chatManager.ChatMessageToOne(
                        ChatChannel.Server,
                        message,
                        wrappedMessage,
                        default,
                        false,
                        player.Channel
                    );
                }

                SpawnGuns(uid, _random.Next(rule.Playercount * 2, rule.Playercount * 4));

                rule.CurrentStatus = TTTStatus.AwaitRolesToAdd; //Вот теперь матч реально начался
            }

            rule.TimeOnNewCycle += TimeSpan.FromSeconds(1);

            //обратный отсчёт
            if (rule.CurrentStatus == TTTStatus.AwaitRolesToAdd)
            {
                if (rule.TimeOnNewCycle <= TimeSpan.FromSeconds(30f))
                {
                    return;
                }
                else
                {
                    int traitorsCount = GetTraitorCount(rule.Playercount);
                    int deccount = GetDecCount(rule.Playercount);
                    var shuffledPlayers = rule.Players.ToList();
                    _random.Shuffle(shuffledPlayers); 
                    foreach (var player in shuffledPlayers)
                    {
                        var filter = Filter.Empty().AddPlayer(player);
                        var message = Loc.GetString("ttt-traitor-brief", ("color", Color.Red));
                        var wrappedMessage = Loc.GetString("chat-manager-server-wrap-message", ("message", message));

                        if (!TryComp<TTTMarkerComponent>(player.AttachedEntity, out var marker))
                            continue;

                        if (marker.Role != TTTRole.await)
                            continue;

                        if (traitorsCount>0)
                        {
                            marker.Role = TTTRole.traitor;
                            AddComp<ShowTTTTraitorsIconsComponent>(player.AttachedEntity.Value);
                            AddComp<TTTTRAITORComponent>(player.AttachedEntity.Value);
                            rule.PlayerCharacters[player.AttachedEntity.Value] = marker.Role; 
                            traitorsCount--;
                            _audio.PlayGlobal("/Audio/Ambience/Antag/traitor_start.ogg", filter, true);
                            _chatManager.ChatMessageToOne(
                                ChatChannel.Server,
                                message,
                                wrappedMessage,
                                default,
                                false,
                                player.Channel
                            );
                            continue;
                        }

                        if ( deccount > 0 && KARMA.TryGetValue(player, out var karma) && karma > 700)
                        {
                            marker.Role = TTTRole.detective;
                            AddComp<TTTDetectiveComponent>(player.AttachedEntity.Value);
                            rule.PlayerCharacters[player.AttachedEntity.Value] = marker.Role; 
                            deccount--;
                            _audio.PlayGlobal("/Audio/Vanilla/Effects/TTT/decbrief.ogg", filter, true);
                            message = Loc.GetString("ttt-detective-brief", ("color", Color.DodgerBlue));
                            wrappedMessage = Loc.GetString("chat-manager-server-wrap-message", ("message", message));
                            _chatManager.ChatMessageToOne(
                                ChatChannel.Server,
                                message,
                                wrappedMessage,
                                default,
                                false,
                                player.Channel
                            );
                            continue;
                        }

                        marker.Role = TTTRole.inocent;
                        rule.PlayerCharacters[player.AttachedEntity.Value] = marker.Role; 
                        _audio.PlayGlobal("/Audio/Vanilla/Effects/TTT/innocentbrief.ogg", filter, true);
                        message = Loc.GetString("ttt-innocent-brief", ("color", Color.Green));
                        wrappedMessage = Loc.GetString("chat-manager-server-wrap-message", ("message", message));
                        _chatManager.ChatMessageToOne(
                            ChatChannel.Server,
                            message,
                            wrappedMessage,
                            default,
                            false,
                            player.Channel
                        );
                    }
                    rule.CurrentStatus = TTTStatus.RoundInProgress;
                    return;
                }
            }
            // Пора запустить новый цикл
            if (rule.CurrentStatus == TTTStatus.RoundInProgress)
            {
                TimeSpan timetoend = rule.TimeToNewCycle - rule.TimeOnNewCycle;

                if (timetoend < TimeSpan.FromMinutes(5) && rule.anoncments == 0)
                {
                    DispatchMonospaceAnnouncement(
                        Filter.Empty().AddPlayers(rule.Players),
                        Loc.GetString("ttt-timetoend-5"),
                        Color.Green);
                    rule.anoncments++;
                    continue;
                }
                if (timetoend < TimeSpan.FromMinutes(3) && rule.anoncments == 1)
                {
                    DispatchMonospaceAnnouncement(
                        Filter.Empty().AddPlayers(rule.Players),
                        Loc.GetString("ttt-timetoend-3"),
                        Color.Yellow);
                    rule.anoncments++;
                    continue;
                }
                if (timetoend < TimeSpan.FromMinutes(1) && rule.anoncments == 2)
                {
                    DispatchMonospaceAnnouncement(
                        Filter.Empty().AddPlayers(rule.Players),
                        Loc.GetString("ttt-timetoend-1"),
                        Color.Red);
                    rule.anoncments++;
                    continue;
                }
                if (rule.TimeOnNewCycle >= rule.TimeToNewCycle)
                    GameOver(uid, rule);
            }
        }
    }

    //обновляем все значения
    public void NewCycle(EntityUid uid, TTTRuleComponent rule)
    {
        rule.Players = new(); //Сбрасываем предыдущих пользователей
        rule.PlayerCharacters = new(); //Сбрасываем предыдущих персонажей
        rule.Playercount = 0;
        rule.CurrentStatus = TTTStatus.awaitstart; //начинаем собирать игроков в раунд
        rule.TimeForPlayersJoin = TimeSpan.FromSeconds(30f);
        rule.TimeOnNewCycle = TimeSpan.FromSeconds(0);
        rule.anoncments = 0;

        //Сообщаем о том что начался новый цикл
        var msg = new TTTInformation(rule.Playercount, rule.TimeForPlayersJoin, rule.CurrentStatus == TTTStatus.awaitstart);
        RaiseNetworkEvent(msg, Filter.Broadcast());
    }

    /// <summary>
    /// Завершает пизделку, сообщает кто победил, запускает новый цикл
    /// </summary>
    private void GameOver(EntityUid uid, TTTRuleComponent rule)
    {
        rule.CurrentStatus = TTTStatus.ended;
        var traitors = rule.PlayerCharacters.Count(p => p.Value == TTTRole.traitor);
        var innocents = rule.PlayerCharacters.Count(p => p.Value == TTTRole.inocent || p.Value == TTTRole.detective);

        bool winner = innocents <= 0; // 1 - предатели, 0 - невиновные

        List<PlayerStats> statsList = new();

        var query = EntityQueryEnumerator<TTTMarkerComponent>();

        while (query.MoveNext(out var unit, out var marker))
        {
            if (!TryComp<TTTRuleComponent>(marker.RuleLink, out var rulelink))
                continue;

            if (rulelink != rule)
                continue;

            if (!TryComp<ActorComponent>(unit, out var actor))
                continue;

            KARMA[actor.PlayerSession] += 50;
                
            var name = actor.PlayerSession.Name;

            statsList.Add(new PlayerStats(name, marker.TotalKills, KARMA[actor.PlayerSession], marker.Role));
        }

        var sorted = statsList
            .OrderByDescending(s => s.Role)
            .ThenByDescending(s => s.Karma)
            .ToList();

        var result = $"{"Игрок".PadRight(32)}| {"Роль".PadRight(10)}| {"Убийств".PadRight(7)}| {"Карма".PadRight(5)}\n";

        foreach (var stat in sorted)
        {
            string name = stat.Name.Length > 32 ? stat.Name[..32] : stat.Name;
            result += name.PadRight(32) + "| " + stat.Role.ToString().PadRight(10) + "| " + stat.Kills.ToString().PadRight(7) + "| " + stat.Karma.ToString().PadRight(5) + "\n";
        }

        var message = Loc.GetString("ttt-gameover",
            ("winner", winner),
            ("result", result));

        DispatchMonospaceAnnouncement(
            Filter.Empty().AddPlayers(rule.Players),
            message,
            winner ? Color.Red : Color.Green);

        //музыка
        var traitorSessions = new List<ICommonSession>();
        var innocentSessions = new List<ICommonSession>();

        foreach (var (ent, role) in rule.PlayerCharacters)
        {
            if (!_playerManager.TryGetSessionByEntity(ent, out var session))
                continue;

            if (role == TTTRole.traitor)
                traitorSessions.Add(session);
            else if (role == TTTRole.inocent || role == TTTRole.detective)
                innocentSessions.Add(session);
        }

        var traitorFilter = Filter.Empty().AddPlayers(traitorSessions);
        var innocentFilter = Filter.Empty().AddPlayers(innocentSessions);

        if (winner)
        {
            _audio.PlayGlobal("/Audio/Vanilla/Effects/TTT/winsound.ogg", traitorFilter, true);
            _audio.PlayGlobal("/Audio/Vanilla/Effects/TTT/losesound.ogg", innocentFilter, true);
        }
        else
        {
            _audio.PlayGlobal("/Audio/Vanilla/Effects/TTT/winsound.ogg", innocentFilter, true);
            _audio.PlayGlobal("/Audio/Vanilla/Effects/TTT/losesound.ogg", traitorFilter, true);
        }
        //Удаляем прошлую арену
        Timer.Spawn(TimeSpan.FromSeconds(1), () => QueueDel(rule.Arena)); 

        NewCycle(uid, rule);
    }

    private void OnDamageModify(EntityUid uid, TTTMarkerComponent component, DamageModifyEvent args)
    {
        if (!TryComp<TTTMarkerComponent>(args.Origin, out var sourcecomp) 
            || !TryComp<ActorComponent>(args.Origin, out var actor) 
            || args.Origin == uid
            || !KARMA.TryGetValue(actor.PlayerSession, out var attackerKarma))
        {
            return;
        }
        if (sourcecomp.Role == TTTRole.await)
        {
            args.Damage = new DamageSpecifier();
            return;
        }
        // 1. Сначала вычисляем изменение кармы
        int damage = (int)args.Damage.GetTotal();
        int karmaChange = 0;

        if (sourcecomp.Role == TTTRole.traitor && component.Role == TTTRole.traitor)
        {
            karmaChange = -2 * damage;
        }
        else if ((sourcecomp.Role == TTTRole.inocent || sourcecomp.Role == TTTRole.detective) 
                && component.Role == TTTRole.inocent)
        {
            karmaChange = -2 * damage;
        }
        else if ((sourcecomp.Role == TTTRole.inocent || sourcecomp.Role == TTTRole.detective) 
                && component.Role == TTTRole.detective)
        {
            karmaChange = -3 * damage;
        }

        var newKarma = attackerKarma + karmaChange;

        KARMA[actor.PlayerSession] = Math.Clamp(newKarma, 0, 1000);

        // 2. Затем применяем модификатор урона на основе новой кармы
        var karmaFraction = Math.Clamp(newKarma / 1000f, 0f, 1f);

        var modify = new DamageModifierSet
        {
            Coefficients = new Dictionary<string, float>
            {
                ["Slash"] = karmaFraction,
                ["Piercing"] = karmaFraction,
                ["Blunt"] = karmaFraction,
                ["Heat"] = karmaFraction,
                ["Shock"] = karmaFraction,
                ["Cold"] = karmaFraction,
                ["Poison"] = karmaFraction,
                ["Radiation"] = karmaFraction,
                ["Asphyxiation"] = karmaFraction,
                ["Bloodloss"] = karmaFraction
            }
        };

        args.Damage = DamageSpecifier.ApplyModifierSet(args.Damage, modify);
    }
    //Метод добавляет игрока на арену
    public void AddPlayerToArena(ICommonSession session, EntityUid ruleEnt)
    {
        if (!TryComp<TTTRuleComponent>(ruleEnt, out var rule) || rule.ArenaMapId == null)
            return;

        _specialRespawn.TryFindRandomTile(rule.Arena, _mapManager.GetMapEntityId(rule.ArenaMapId.Value), 10, out var targetCoords);

        if (targetCoords == null)
            targetCoords = Transform(rule.Arena).Coordinates;

        var profile = _gameTicker.GetPlayerProfile(session);
        var mobUid = _spawning.SpawnPlayerMob(targetCoords, null, profile, null);

        if (_mindSystem.TryGetMind(session.AttachedEntity!.Value, out var mindId, out var mindComp))
            _mindSystem.TransferTo(mindId, mobUid, true, mind: mindComp);

        //Добавляем метку
        var marker = EnsureComp<TTTMarkerComponent>(mobUid);
        marker.RuleLink = ruleEnt;
        //Все видят детектива
        AddComp<ShowTTTDetectiveIconsComponent>(mobUid);
        //Добавляем навыки
        var skill = EnsureComp<SkillComponent>(mobUid);
        skill.FuckSkills(false);
        //одеваем
        List<ProtoId<StartingGearPrototype>> gear = new()
        {
            "TTTGearInnocent"
        };

        _loadout.Equip(mobUid, gear, null);
        rule.PlayerCharacters[mobUid] = marker.Role; //Добавляем в список игроков
    }

    private void OnMobStateChanged(EntityUid uid, TTTMarkerComponent component, MobStateChangedEvent args)
    {
        //Мгновенно убиваем критованного
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

        if (!TryComp<TTTRuleComponent>(component.RuleLink, out var rulecomp))
            return;

        rulecomp.PlayerCharacters.Remove(uid);

        Color color = component.Role == TTTRole.traitor ? Color.Red : Color.DodgerBlue;

        if (args.Origin != null && TryComp<TTTMarkerComponent>(args.Origin, out var sourcecomp))
        {
            // пред убил преда
            if (sourcecomp.Role == TTTRole.traitor && component.Role == TTTRole.traitor)
            {
                sourcecomp.TotalKills--;
            }
            // Мирный или детектив убил мирного или детектива
            else if ((sourcecomp.Role == TTTRole.inocent || sourcecomp.Role == TTTRole.detective) 
                    && (component.Role == TTTRole.inocent || component.Role == TTTRole.detective))
            {
                sourcecomp.TotalKills--; 
            }
            else if (args.Origin.Value != uid)
            {
                sourcecomp.TotalKills++;  // Награда за убийство врага
            }
        }

        var traitors = rulecomp.PlayerCharacters.Count(p => p.Value == TTTRole.traitor);
        var innocents = rulecomp.PlayerCharacters.Count(p => p.Value == TTTRole.inocent || p.Value == TTTRole.detective);

        if (traitors <= 0 || innocents <= 0)
            GameOver(component.RuleLink.Value, rulecomp);
    }

    private EntityUid? SpawnArena(int playerCount, TTTRuleComponent rule)
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
    private void OnMarkerInit(EntityUid uid, TTTMarkerComponent marker, MapInitEvent args)
    {
        marker.RuleLink = Currentrule;
    }

    private void OnRuleInit(EntityUid uid, TTTRuleComponent rule, MapInitEvent args)
    {
        Currentrule = uid;
        var msg = new TTTInformation(rule.Playercount, rule.TimeForPlayersJoin, rule.CurrentStatus == TTTStatus.awaitstart);
        RaiseNetworkEvent(msg, Filter.Broadcast());
    }

    private void OnRuleShutDown(EntityUid uid, TTTRuleComponent component, ComponentShutdown args)
    {
        GameOver(uid, component);
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

    private void SpawnGuns(EntityUid ruleEnt, int count)
    {
        if (!TryComp<TTTRuleComponent>(ruleEnt, out var rule) || rule.ArenaMapId == null)
            return;

        for (var i = 0; i < count; i++)
        {
            if (!_specialRespawn.TryFindRandomTile(rule.Arena, _mapManager.GetMapEntityId(rule.ArenaMapId.Value), 10, out var targetCoords))
                continue;

            Spawn(_random.Pick(GUNS), targetCoords);
        }
    }
    int GetTraitorCount(int playerCount)
    {
        if (playerCount < 8)
            return 1;
        if (playerCount < 12)
            return 2;
        if (playerCount < 16)
            return 3;
        return playerCount / 4;
    }
    int GetDecCount(int playerCount)
    {
        if (playerCount < 6)
            return 0;
        if (playerCount < 12)
            return 1;
        return 2;
    }
}
