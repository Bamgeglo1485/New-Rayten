using Content.Server.Mind;
using Content.Server.Chat.Managers;
using Content.Server.Respawn;
using Content.Shared.Chat;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Vanilla.Skill;
using Content.Shared.Clothing;
using Content.Shared.Damage.Systems;
using Content.Shared.Damage;
using Content.Shared.Mobs;
using Content.Shared.Ghost;
using Content.Shared.Roles;
using Content.Shared.Implants;
using Content.Shared.Preferences;
using Robust.Server.GameObjects;
using Robust.Shared.Utility;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.EntitySerialization;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Timer = Robust.Shared.Timing.Timer;
using Robust.Shared.Player;
using Robust.Shared.Random;
using System.Linq;

using Content.Shared.Vanilla.TDM;
using Content.Shared.Vanilla.Games.TTT;
using Content.Shared.Body.Components;

namespace Content.Server.Vanilla.Games.TTT;

public sealed class TTTSystem : EntitySystem
{

    [Dependency] private readonly SharedSkillSystem _skill = default!;
    private static readonly string[] GUNS = new[]
    {
        "Musket", "WeaponPistolFlintlock", "WeaponLaserSvalinn", "WeaponDominator", "WeaponEnergyShotgun", "WeaponAssaultDominator", "MakeshiftShield",
        "WeaponMakeshiftLaser", "WeaponLaserCarbinePractice", "WeaponLaserCarbine",
        "WeaponLaserCannon", "WeaponPistolViper", "WeaponPistolCobra", "WeaponPistolMk58", "WeaponPistolN1984",
        "WeaponRevolverDeckard","WeaponRevolverInspector","WeaponRevolverMateba","WeaponRevolverPython","WeaponRevolverPirate","WeaponRifleLecter","WeaponRifleEstoc","WeaponRifleFoam","WeaponShotgunDoubleBarreled",
        "WeaponShotgunKammerer","WeaponShotgunSawn","WeaponShotgunHandmade","WeaponShotgunBlunderbuss","WeaponShotgunImprovised","WeaponSubMachineGunC20r","WeaponSubMachineGunDrozd","WeaponSubMachineGunWt550","WeaponImprovisedPneumaticCannon"
    };
    private readonly Dictionary<ICommonSession, int> _kARMA = new();

    private sealed class PlayerStats
    {
        public string Name { get; set; }
        public int Kills { get; set; }
        public int Karma { get; set; }
        public string Role { get; set; }

        public PlayerStats(string name, int kills, int karma, string role)
        {
            Name = name;
            Kills = kills;
            Karma = karma;
            Role = role;
        }
    }

    [Dependency] private readonly MapSystem _mapSystem = default!;
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly MindSystem _mindSystem = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedGhostSystem _ghosts = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly SpecialRespawnSystem _specialRespawn = default!;
    [Dependency] private readonly LoadoutSystem _loadout = default!;
    [Dependency] private readonly SharedSubdermalImplantSystem _implant = default!;
    [Dependency] private readonly MetaDataSystem _metaSystem = default!;
    private EntityUid? _currentrule = null;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TTTMarkerComponent, DamageModifyEvent>(OnDamageModify);    //Урон в зависимости от крамы
        SubscribeLocalEvent<TTTMarkerComponent, DamageChangedEvent>(OnDamageChange);    //Начисляем бонусы в виде кармы за урон
        SubscribeLocalEvent<TTTMarkerComponent, MobStateChangedEvent>(OnMobStateChanged); //Вычёркиваем

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

        if (session == null || _currentrule == null)
            return;

        if (!TryComp<TTTRuleComponent>(_currentrule, out var rule))
            return;

        if (rule.CurrentStatus != TTTStatus.awaitstart)
            return;

        if (!_kARMA.ContainsKey(session))
        {
            _kARMA[session] = 1000;
        }

        else if (_kARMA[session] <= 0)
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
        if (session.AttachedEntity != null && TryComp<GhostComponent>(session.AttachedEntity, out var ghost))
            _ghosts.SetCanReturnToBody((session.AttachedEntity.Value, ghost), false);
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs e)
    {
        if (e.NewStatus != SessionStatus.Disconnected || _currentrule == null || !TryComp<TTTRuleComponent>(_currentrule, out var rule))
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
        if (_currentrule != null)
        {
            if (!TryComp<TTTRuleComponent>(_currentrule, out var rule))
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

                SpawnGuns(uid, _random.Next(rule.Playercount, rule.Playercount * 3));

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

                        if (!player.AttachedEntity.HasValue)
                            continue;

                        var playerent = player.AttachedEntity.Value;

                        if (!TryComp<TTTMarkerComponent>(playerent, out var marker))
                            continue;

                        if (marker.Role != TTTRole.await)
                            continue;

                        if (!_kARMA.TryGetValue(player, out var karma))
                            continue;

                        if (traitorsCount > 0)
                        {
                            _implant.AddImplant(playerent, "TraitorShopImplant");
                            marker.Role = TTTRole.traitor;
                            AddComp<ShowTTTTraitorsComponent>(playerent);
                            AddComp<TTTTRAITORComponent>(playerent);
                            rule.PlayerCharacters[playerent] = marker.Role;
                            traitorsCount--;
                            _audio.PlayGlobal(rule.TraitorBrief, filter, true);
                            _chatManager.ChatMessageToOne(
                                ChatChannel.Server,
                                message,
                                wrappedMessage,
                                default,
                                false,
                                player.Channel
                            );
                            Dirty(playerent, marker);
                            continue;
                        }

                        if (deccount > 0 && karma > 700)
                        {
                            _implant.AddImplant(playerent, "DetectiveShopImplant");
                            marker.Role = TTTRole.detective;
                            rule.PlayerCharacters[playerent] = marker.Role;
                            deccount--;
                            _audio.PlayGlobal(rule.DecBrief, filter, true);
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
                            Dirty(playerent, marker);
                            if (TryComp<NameOverlayComponent>(playerent, out var nameMarker))
                            {
                                nameMarker.NameColor = Color.DodgerBlue;
                                Dirty(playerent, nameMarker);
                            }

                            continue;
                        }

                        marker.Role = TTTRole.inocent;
                        rule.PlayerCharacters[player.AttachedEntity.Value] = marker.Role;
                        _audio.PlayGlobal(rule.InoBrief, filter, true);
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
                        Dirty(playerent, marker);
                    }
                    rule.CurrentStatus = TTTStatus.RoundInProgress;
                    return;
                }
            }
            // Пора запустить новый цикл
            if (rule.CurrentStatus == TTTStatus.RoundInProgress)
            {
                TimeSpan timetoend = rule.TimeToNewCycle - rule.TimeOnNewCycle;

                if (timetoend < TimeSpan.FromMinutes(5) && rule.Anoncments == 0)
                {
                    SpawnGuns(uid, _random.Next(rule.Playercount, rule.Playercount * 2));
                    DispatchMonospaceAnnouncement(
                        Filter.Empty().AddPlayers(rule.Players),
                        Loc.GetString("ttt-timetoend-5"),
                        Color.Green);
                    rule.Anoncments++;
                    continue;
                }
                if (timetoend < TimeSpan.FromMinutes(3) && rule.Anoncments == 1)
                {
                    SpawnGuns(uid, _random.Next(rule.Playercount, rule.Playercount * 2));
                    DispatchMonospaceAnnouncement(
                        Filter.Empty().AddPlayers(rule.Players),
                        Loc.GetString("ttt-timetoend-3"),
                        Color.Yellow);
                    rule.Anoncments++;
                    continue;
                }
                if (timetoend < TimeSpan.FromMinutes(1) && rule.Anoncments == 2)
                {
                    SpawnGuns(uid, _random.Next(rule.Playercount, rule.Playercount * 2));
                    DispatchMonospaceAnnouncement(
                        Filter.Empty().AddPlayers(rule.Players),
                        Loc.GetString("ttt-timetoend-1"),
                        Color.Red);
                    rule.Anoncments++;
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
        rule.Anoncments = 0;

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


        var traitorSessions = new List<ICommonSession>();
        var innocentSessions = new List<ICommonSession>();
        var query = EntityQueryEnumerator<TTTMarkerComponent>();
        while (query.MoveNext(out var unit, out var marker))
        {
            if (!TryComp<TTTRuleComponent>(marker.RuleLink, out var rulelink))
                continue;

            if (rulelink != rule)
                continue;

            _kARMA[marker.Session] = Math.Clamp(_kARMA[marker.Session] + 50, -51, 1500);

            statsList.Add(new PlayerStats(marker.Session.Name, marker.TotalKills, _kARMA[marker.Session], marker.GetRoleName()));
            //музыка
            if (marker.Role == TTTRole.traitor)
                traitorSessions.Add(marker.Session);
            else if (marker.Role == TTTRole.inocent || marker.Role == TTTRole.detective)
                innocentSessions.Add(marker.Session);
        }

        var sorted = statsList
            .OrderByDescending(s => s.Role)
            .ThenByDescending(s => s.Karma)
            .ToList();

        var result = $"{"Игрок".PadRight(32)}| {"Роль".PadRight(10)}| {"Убийств".PadRight(7)}| {"Карма".PadRight(5)}\n";

        foreach (var stat in sorted)
        {
            string name = stat.Name.Length > 32 ? stat.Name[..32] : stat.Name;
            result += name.PadRight(32) + "| " + stat.Role.PadRight(10) + "| " + stat.Kills.ToString().PadRight(7) + "| " + stat.Karma.ToString().PadRight(5) + "\n";
        }

        var message = Loc.GetString("ttt-gameover",
            ("winner", winner),
            ("result", result));

        DispatchMonospaceAnnouncement(
            Filter.Empty().AddPlayers(rule.Players),
            message,
            winner ? Color.Red : Color.Green);

        var traitorFilter = Filter.Empty().AddPlayers(traitorSessions);
        var innocentFilter = Filter.Empty().AddPlayers(innocentSessions);

        if (winner)
        {
            _audio.PlayGlobal(rule.WinSound, traitorFilter, false);
            _audio.PlayGlobal(rule.LoseSound, innocentFilter, false);
        }
        else
        {
            _audio.PlayGlobal(rule.WinSound, innocentFilter, false);
            _audio.PlayGlobal(rule.LoseSound, traitorFilter, false);
        }
        //Удаляем прошлую арену
        Timer.Spawn(TimeSpan.FromSeconds(1), () => QueueDel(rule.Arena));

        NewCycle(uid, rule);
    }

    private void OnDamageChange(EntityUid uid, TTTMarkerComponent component, DamageChangedEvent args)
    {
        if (!args.DamageIncreased
            || args.DamageDelta == null
            || args.DamageDelta.GetTotal() <= 0
            || !TryComp<TTTMarkerComponent>(args.Origin, out var sourcecomp)
            || args.Origin == uid
            || !_kARMA.TryGetValue(sourcecomp.Session, out var attackerKarma))
        {
            return;
        }

        var damage = (int)args.DamageDelta.GetTotal();
        var karmaChange = 0;

        if (sourcecomp.Role == TTTRole.traitor && component.Role == TTTRole.traitor)
            karmaChange = -5 * damage;
        else if ((sourcecomp.Role == TTTRole.inocent || sourcecomp.Role == TTTRole.detective)
                && component.Role == TTTRole.inocent)
            karmaChange = -5 * damage;
        else if ((sourcecomp.Role == TTTRole.inocent || sourcecomp.Role == TTTRole.detective)
                && component.Role == TTTRole.detective)
            karmaChange = -7 * damage;

        _kARMA[sourcecomp.Session] = Math.Clamp(attackerKarma + karmaChange, -500, 1500);
    }

    private void OnDamageModify(EntityUid uid, TTTMarkerComponent component, DamageModifyEvent args)
    {
        if (!TryComp<TTTMarkerComponent>(args.Origin, out var sourcecomp)
            || args.Origin == uid
            || !_kARMA.TryGetValue(sourcecomp.Session, out var attackerKarma))
        {
            return;
        }

        if (sourcecomp.Role == TTTRole.await)
        {
            args.Damage = new DamageSpecifier();
            return;
        }

        //у предателей и детективов урон не уменьшается
        if (sourcecomp.Role == TTTRole.traitor || sourcecomp.Role == TTTRole.detective)
            return;

        //  применяем модификатор урона на основе новой кармы
        var karmaFraction = Math.Clamp(attackerKarma / 1000f, 0.01f, 1f);

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

        _specialRespawn.TryFindRandomTile(rule.Arena, _mapSystem.GetMap(rule.ArenaMapId.Value), 10, out var targetCoords);

        if (!_prototypeManager.TryIndex<SpeciesPrototype>(HumanoidCharacterProfile.DefaultSpecies, out var species))
            throw new ArgumentException($"Invalid species prototype was used: {HumanoidCharacterProfile.DefaultSpecies}");

        var mobUid = Spawn(species.Prototype, targetCoords);
        _metaSystem.SetEntityName(mobUid, session.Name);

        if (_mindSystem.TryGetMind(session.AttachedEntity!.Value, out var mindId, out var mindComp))
            _mindSystem.TransferTo(mindId, mobUid, true, mind: mindComp);

        //Добавляем метку
        var marker = EnsureComp<TTTMarkerComponent>(mobUid);
        marker.RuleLink = ruleEnt;
        marker.Session = session;
        var nameMarker = EnsureComp<NameOverlayComponent>(mobUid);
        nameMarker.Name = session.Name;
        //Все видят детектива
        AddComp<ShowTTTDetectiveIconsComponent>(mobUid);
        //Добавляем навыки
        var skill = EnsureComp<SkillComponent>(mobUid);
        _skill.FuckSkills(mobUid, skill);
        //одеваем
        List<ProtoId<StartingGearPrototype>> gear = new()
        {
            "TTTGearInnocent"
        };
        //bloodstream
        if (TryComp<BloodstreamComponent>(mobUid, out var blood))
            blood.MaxBleedAmount = 0;
        _loadout.Equip(mobUid, gear, null);
        rule.PlayerCharacters[mobUid] = marker.Role; //Добавляем в список игроков
    }

    private void OnMobStateChanged(EntityUid uid, TTTMarkerComponent component, MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Alive)
            return;

        if (args.OldMobState >= MobState.Critical)
            return;

        _damageable.TryChangeDamage(uid, component.Damage, true, false, null);

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

    private void OnRuleInit(EntityUid uid, TTTRuleComponent rule, MapInitEvent args)
    {
        _currentrule = uid;
        var msg = new TTTInformation(rule.Playercount, rule.TimeForPlayersJoin, rule.CurrentStatus == TTTStatus.awaitstart);
        RaiseNetworkEvent(msg, Filter.Broadcast());
    }

    private void OnRuleShutDown(EntityUid uid, TTTRuleComponent component, ComponentShutdown args)
    {
        GameOver(uid, component);
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

    private void SpawnGuns(EntityUid ruleEnt, int count)
    {
        if (!TryComp<TTTRuleComponent>(ruleEnt, out var rule) || rule.ArenaMapId == null)
            return;

        for (var i = 0; i < count; i++)
        {
            if (!_specialRespawn.TryFindRandomTile(rule.Arena, _mapSystem.GetMap(rule.ArenaMapId.Value), 10, out var targetCoords))
                continue;

            Spawn(_random.Pick(GUNS), targetCoords);
        }
    }
    int GetTraitorCount(int playerCount)
    {
        if (playerCount < 8)
            return 1;
        if (playerCount < 13)
            return 2;
        if (playerCount < 17)
            return 3;
        if (playerCount < 21)
            return 4;
        if (playerCount < 25)
            return 5;
        return playerCount / 4;
    }
    int GetDecCount(int playerCount)
    {
        if (playerCount < 6)
            return 0;
        if (playerCount < 14)
            return 1;
        if (playerCount < 21)
            return 2;
        if (playerCount < 25)
            return 3;
        return 4;
    }
}
