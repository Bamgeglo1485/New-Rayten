using Content.Server.GameTicking;
using Content.Server.Mind;
using Content.Server.Station.Systems;
using Content.Server.Spawners.Components;
using Content.Server.Chat.Systems;
using Content.Shared.Administration;
using Content.Shared.GameTicking;
using Content.Shared.Vanilla.TDMRoundEnd;
using Content.Shared.Vanilla.CCVars;
using Content.Shared.Vanilla.Skill;
using Content.Shared.Mindshield.Components;
using Content.Shared.Clothing;
using Content.Shared.Damage;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.IdentityManagement;
using Robust.Server.GameObjects;
using Robust.Shared.Utility;
using Robust.Server.Player;
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

namespace Content.Server.Vanilla.TDM;

public sealed class TDMSystem : EntitySystem
{

    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly MapSystem _mapSystem = default!;
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly MindSystem _mindSystem = default!;
    [Dependency] private readonly StationSpawningSystem _spawning = default!;
    [Dependency] private readonly LoadoutSystem _loadout = default!;
    [Dependency] protected readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ChatSystem _chatSystem = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    private int Blueguys = 0;
    private int Redguys = 0;
    private bool onlyonecycle = false;
    private bool firstblood = false;
    private MapId? _arenaMapId = null;
    private EntityUid? PreviousGrid = null;
    private EntityUid? TDMUID = null;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TDMMarkerComponent, DamageChangedEvent>(OnDamageChanged, before: [typeof(MobThresholdSystem)]);
        SubscribeLocalEvent<TDMMarkerComponent, DamageModifyEvent>(OnDamageModify);
        SubscribeLocalEvent<TDMMarkerComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<RoundStartedEvent>(OnRoundStarted);
        SubscribeLocalEvent<TDMRuleComponent, NewTDMCycleEvent>(NewCycle);
    }
    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var currentTime = _gameTiming.CurTime;

        if (TDMUID == null)
            return;

        if (!TryComp<TDMRuleComponent>(TDMUID.Value, out var rule))
            return;

        if (currentTime < rule.NextUpdate)
            return;

        // Обновляем таймер на следующий апдейт
        rule.NextUpdate = currentTime + TimeSpan.FromSeconds(1);

        rule.TimeOnNewCycle += TimeSpan.FromSeconds(1);

        // Обратный отсчёт
        if (!rule.CountdownPlayed && rule.TimeOnNewCycle >= TimeSpan.FromSeconds(19.5))
        {
            rule.CountdownPlayed = true;
            if (Redguys <= 0 || Blueguys <= 0)
            {
                GameOver();
                return;
            }
            var allPlayersInGame = Filter.Empty().AddWhere(_gameTicker.UserHasJoinedGame);
            _audio.PlayGlobal("/Audio/Vanilla/Effects/TDM/counting.ogg", allPlayersInGame, true);
        }

        // Пора запустить новый цикл
        if (!rule.GameOverPlayed && rule.TimeOnNewCycle >= rule.TimeToNewCycle)
        {
            rule.GameOverPlayed = true;
            GameOver();
        }

    }

    private void NewCycle(EntityUid uid, TDMRuleComponent rule, NewTDMCycleEvent args)
    {
        TDMUID = uid;
        rule.TimeOnNewCycle = TimeSpan.FromSeconds(0);
        rule.GameOverPlayed = false;
        rule.CountdownPlayed = false;
        Blueguys = 0;
        Redguys = 0;
        onlyonecycle = rule.OnlyOneCycle;

        int playerCount = 0;
        bool odd = false;
        bool team = false; //1 - red 0 - blue

        //считаем количество игроков
        foreach (var session in _playerManager.Sessions)
        {
            if (!session.AttachedEntity.HasValue)
                continue;
            playerCount++;
        }
        if (playerCount % 2 == 1)
        {
            odd = true;
            playerCount -= 1;
        }
        firstblood = false;

        //выбираем рандомный прототип арены и спавним грид арены
        var proto = PickRandomArena(playerCount);
        if (proto == null)
        {
            Log.Error($"Не удалось найти никакой подходящей карты");
            _gameTicker.RestartRound();
            return;
        }

        var arena = SpawnArena(proto.ArenaPath);

        if (arena == null)
        {
            Log.Error($"Арена не заспавнилась.");
            _gameTicker.RestartRound();
            return;
        }

        HashSet<EntityUid> usedspawners = new();
        //Манипуляции с игроками
        var sessions = _playerManager.Sessions;
        _random.Shuffle(sessions);
        foreach (var session in sessions)
        {
            if (!session.AttachedEntity.HasValue)
                continue;

            if (odd)
            {
                odd = false;
                continue;
            }
            var entityId = tptoarena(session, arena.Value, team, usedspawners);
            AddComp<AdminFrozenComponent>(entityId);

            if (team)
                Redguys++;
            else
                Blueguys++;

            var marker = EnsureComp<TDMMarkerComponent>(entityId);
            marker.Team = team;

            entityId.SpawnTimer(TimeSpan.FromSeconds(30), () => RemComp<AdminFrozenComponent>(entityId));
            fuckskills(entityId);
            _loadout.Equip(entityId, team ? proto.RedTeamGear : proto.BlueTeamGear, null);
            //меняем команду для следующего игрока
            team = !team;
        }
        PreviousGrid = arena;
    }
    private void GameOver()
    {
        QueueDel(PreviousGrid);
        if (TDMUID == null)
            return;

        if (onlyonecycle)
        {
            Timer.Spawn(TimeSpan.FromSeconds(5), () => _gameTicker.RestartRound());
        }
        else
        {
            Timer.Spawn(TimeSpan.FromSeconds(10), () => RaiseLocalEvent(TDMUID.Value, new NewTDMCycleEvent()));
        }

        bool winner = Blueguys > Redguys ? false : true;

        if (Blueguys == Redguys)
            _chatSystem.DispatchGlobalAnnouncement(
                Loc.GetString("tdm-gameover", ("winner", "other")),
                Loc.GetString("tdm-announcer"),
                playSound: false,
                null,
                Color.Green
            );
        else
            _chatSystem.DispatchGlobalAnnouncement(
                Loc.GetString("tdm-gameover", ("winner", winner)),
                Loc.GetString("tdm-announcer"),
                playSound: false,
                null,
                winner ? Color.Red : Color.DodgerBlue
            );
    }
    private void OnRoundStarted(RoundStartedEvent ev)
    {
        EntityUid? lastid = null;
        var query = EntityQueryEnumerator<TDMRuleComponent>();
        while (query.MoveNext(out var uid, out var rule))
        {
            lastid = uid;
        }

        if (lastid != null)
        {
            _arenaMapId = null;
            TDMUID = lastid;
            RaiseLocalEvent(lastid.Value, new NewTDMCycleEvent());
        }
    }

    private void OnDamageModify(EntityUid uid, TDMMarkerComponent component, DamageModifyEvent args)
    {
        if (!TryComp<TDMMarkerComponent>(args.Origin, out var sourcecomp))
            return;

        if (component.Team != sourcecomp.Team)
            return;

        // Полностью обнуляем урон
        args.Damage = new DamageSpecifier();
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
        if (args.NewMobState == MobState.Critical && args.OldMobState < args.NewMobState)
        {
            if (TryComp<DamageableComponent>(uid, out var damage))
            {
                _damageable.TryChangeDamage(uid, component.Damage, true, false, damage);
            }
            else return;
        }
        else return;

        if (component.Team)
            Redguys -= 1;
        else
            Blueguys -= 1;

        if (Blueguys <= 0 || Redguys <= 0)
            GameOver();

        Color color = component.Team ? Color.DodgerBlue : Color.Red;

        if (args.Origin == null)
            return;

        var origin = args.Origin.Value;
        TryComp<TDMMarkerComponent>(origin, out var sourcecomp);
        if (sourcecomp == null)
            return;

        if (origin != uid)
            sourcecomp.TotalKills++;

        if (!TryComp<MetaDataComponent>(origin, out var originmeta) || !TryComp<MetaDataComponent>(uid, out var entmeta))
            return;

        string sourcename = originmeta.EntityName;
        string victimname = entmeta.EntityName;
        if (!firstblood)
        {
            firstblood = true;
            _chatSystem.DispatchGlobalAnnouncement(
                Loc.GetString("tdm-firstblood", ("player", sourcename), ("victim", victimname)),
                Loc.GetString("tdm-announcer"),
                playSound: true,
                new SoundPathSpecifier("/Audio/Vanilla/Effects/TDM/Firstblood.ogg"),
                color
            );
            return;
        }

        var kills = sourcecomp.TotalKills;
        var killSounds = new Dictionary<int, string>
        {
            { 2, "/Audio/Vanilla/Effects/TDM/Doublekill.ogg" },
            { 3, "/Audio/Vanilla/Effects/TDM/TripleKill.ogg" },
            { 4, "/Audio/Vanilla/Effects/TDM/UltraKill.ogg" },
            { 5, "/Audio/Vanilla/Effects/TDM/Rampage.ogg" },
        };
        var playSound = killSounds.ContainsKey(kills);
        var sound = playSound ? new SoundPathSpecifier(killSounds[kills]) : null;

        _chatSystem.DispatchGlobalAnnouncement(
            Loc.GetString("tdm-killstreak", ("streak", kills), ("player", sourcename), ("victim", victimname)),
            Loc.GetString("tdm-announcer"),
            playSound: playSound,
            sound,
            color
        );
    }
    //респавнит челика на арене
    private EntityUid tptoarena(ICommonSession session, EntityUid arena, bool team, HashSet<EntityUid> usedspawners)
    {
        var coords = FindSpawnCoordinates(arena, team, usedspawners);
        var profile = _gameTicker.GetPlayerProfile(session);
        var mobUid = _spawning.SpawnPlayerMob(coords, null, profile, null);

        if (_mindSystem.TryGetMind(session.AttachedEntity!.Value, out var mindId, out var mindComp))
            _mindSystem.TransferTo(mindId, mobUid, true, mind: mindComp);
        return mobUid;
    }
    private EntityCoordinates FindSpawnCoordinates(EntityUid arena, bool team, HashSet<EntityUid> usedspawners)
    {
        var targetSpawnId = team ? "SpawnPointTeamRed" : "SpawnPointTeamBlue";
        EntityCoordinates? lastvalidspawner = null;

        var query = EntityQueryEnumerator<SpawnPointComponent, MetaDataComponent, TransformComponent>();
        while (query.MoveNext(out var entity, out _, out var meta, out var trans))
        {
            if (meta.EntityPrototype?.ID != targetSpawnId)
                continue;

            if (trans.GridUid != arena)
                continue;

            if (usedspawners.Contains(entity))
                continue;

            usedspawners.Add(entity);
            lastvalidspawner = trans.Coordinates;
            return trans.Coordinates;
        }
        return lastvalidspawner ?? Transform(arena).Coordinates;
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

    private EntityUid? SpawnArena(string arenaPath)
    {
        // Проверка: если карта не существует, создаём новую
        if (_arenaMapId == null || !_mapSystem.MapExists(_arenaMapId.Value))
        {
            _mapSystem.CreateMap(out var newMapId);
            _arenaMapId = newMapId;
        }

        var opts = DeserializationOptions.Default;

        // Пытаемся загрузить грид на карту
        if (!_mapLoader.TryLoadGrid(_arenaMapId.Value, new ResPath(arenaPath), out var grid, opts))
        {
            Logger.Warning($"Не удалось загрузить арену по пути {arenaPath} на карту {_arenaMapId.Value}");
            return null;
        }

        return grid;
    }

    private void fuckskills(EntityUid user)
    {
        if (!TryComp<SkillComponent>(user, out var skillComp))
            skillComp = EnsureComp<SkillComponent>(user);

        // Присваиваем максимальные уровни всем навыкам
        skillComp.Piloting = true;
        skillComp.MusInstruments = true;
        skillComp.Botany = true;
        skillComp.Bureaucracy = true;
        skillComp.Atmosphere = true;
        skillComp.RangeWeaponLevel = SkillLevel.Expert;
        skillComp.MeleeWeaponLevel = SkillLevel.Expert;
        skillComp.MedicineLevel = SkillLevel.Expert;
        skillComp.ChemistryLevel = SkillLevel.Expert;
        skillComp.EngineeringLevel = SkillLevel.Expert;
        skillComp.BuildingLevel = SkillLevel.Expert;
        skillComp.ResearchLevel = SkillLevel.Expert;

        Dirty(user, skillComp);
    }
}

public sealed class NewTDMCycleEvent : EntityEventArgs
{
}
