using Content.Server.GameTicking;
using Content.Server.Mind;
using Content.Server.Station.Systems;
using Content.Server.Spawners.Components;
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
using Robust.Server.GameObjects;
using Robust.Shared.Utility;
using Robust.Server.Player;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.EntitySerialization;
using Robust.Shared.Map.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Map;
using Robust.Shared.Configuration;
using Timer = Robust.Shared.Timing.Timer;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Server.Vanilla.TDMRoundEnd;

public sealed class TDMRoundEndSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly MapSystem _mapsystem = default!;
    [Dependency] private readonly MapLoaderSystem _map = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly MindSystem _mindSystem = default!;
    [Dependency] private readonly StationSpawningSystem _spawning = default!;
    [Dependency] private readonly LoadoutSystem _loadout = default!;
    [Dependency] protected readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private bool _isEnabled = false;
    public int Blueguys = 0;
    public int Redguys = 0;
    public bool IsTDMPlaying = false;
    public override void Initialize()
    {
        base.Initialize();
        _cfg.OnValueChanged(CCVVars.TDMRoundEndEnabled, v => _isEnabled = v, true);
        SubscribeLocalEvent<RoundEndTextAppendEvent>(OnRoundEnded);
        SubscribeLocalEvent<TDMMarkerComponent, DamageChangedEvent>(OnDamageChanged, before: [typeof(MobThresholdSystem)]);
        SubscribeLocalEvent<TDMMarkerComponent, MobStateChangedEvent>(OnMobStateChanged);
    }
    private void OnMobStateChanged(EntityUid uid, TDMMarkerComponent component, MobStateChangedEvent args)
    {
        if (!IsTDMPlaying)
            return;

        if (args.NewMobState != MobState.Dead || args.OldMobState >= args.NewMobState)
            return;

        if (component.Team)
            Redguys--;
        else
            Blueguys--;

        if (Blueguys <= 0 || Redguys <= 0)
        {
            GameOver();
        }
        if (args.Origin != null && args.Origin.Value != uid && TryComp<TDMMarkerComponent>(args.Origin, out var sourcecomp))
            sourcecomp.TotalKills++;
    }

    private void GameOver()
    {
        IsTDMPlaying = false;
    }

    private void OnDamageChanged(EntityUid uid, TDMMarkerComponent component, DamageChangedEvent args)
    {
        if (!args.DamageIncreased || args.DamageDelta == null)
            return;

        if (args.Origin != null && args.Origin.Value != uid && TryComp<TDMMarkerComponent>(args.Origin, out var sourcecomp))
            sourcecomp.TotalDamage += args.DamageDelta.GetTotal();
    }

    private void OnRoundEnded(RoundEndTextAppendEvent ev)
    {
        if (!_isEnabled)
            return;

        Filter allPlayersInGame = Filter.Empty().AddWhere(_gameTicker.UserHasJoinedGame);
        int playerCount = 0;
        bool odd = false;;
        bool team = false; //1 - red 0 - blue

        //считаем количество игроков
        foreach (var session in _playerManager.Sessions)
        {
            if (!session.AttachedEntity.HasValue) continue;
            playerCount++;
        }
        if (playerCount % 2 == 1)
        {
            odd = true;
            playerCount -= 1;
        }

        Blueguys = playerCount/2;
        Redguys = playerCount/2;

        //выбираем рандомный прототип арены и спавним грид арены
        var proto = PickRandomArena(playerCount);
        if (proto == null)
        {
            Log.Error($"Не удалось найти никакой подходящей карты");
            GameOver();
            return;
        }

        var arena = SpawnArena(proto.ArenaPath);

        if (arena == null)
        {
            Log.Error($"Арена не заспавнилась.");
            GameOver();
            return;
        }

        if (playerCount <= 0)
        {
            GameOver();
            return;
        }
        //Манипуляции с игроками
        foreach (var session in _playerManager.Sessions)
        {
            if (!session.AttachedEntity.HasValue)
                continue;
            if (odd)
            {
                odd = false;
                continue;
            }
            var entityId = tptoarena(session, arena.Value, team);
            AddComp<AdminFrozenComponent>(entityId);

            var marker = EnsureComp<TDMMarkerComponent>(entityId);
            marker.Team = team;

            entityId.SpawnTimer(TimeSpan.FromSeconds(30), () => RemComp<AdminFrozenComponent>(entityId));
            fuckskills(entityId);
            _loadout.Equip(entityId, team ? proto.RedTeamGear : proto.BlueTeamGear, null);
            //меняем команду для следующего игрока
            team = !team;
        }

        IsTDMPlaying = true;
        Timer.Spawn(TimeSpan.FromSeconds(19), () =>
        {
            _audio.PlayGlobal("/Audio/Vanilla/Effects/TDM/counting.ogg", allPlayersInGame, true);
        });

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
    //респавнит челика на арене
    private EntityUid tptoarena(ICommonSession session, EntityUid arena, bool team)
    {
        var coords = FindSpawnCoordinates(arena, team);
        var profile = _gameTicker.GetPlayerProfile(session);
        var mobUid = _spawning.SpawnPlayerMob(coords, null, profile, null);

        if (_mindSystem.TryGetMind(session.AttachedEntity!.Value, out var mindId, out var mindComp))
            _mindSystem.TransferTo(mindId, mobUid, true, mind: mindComp);
        return mobUid;
    }
    private EntityCoordinates FindSpawnCoordinates(EntityUid arena, bool team)
    {
        var targetSpawnId = team ? "SpawnPointTeamRed" : "SpawnPointTeamBlue";

        var query = EntityQueryEnumerator<SpawnPointComponent, MetaDataComponent, TransformComponent>();
        while (query.MoveNext(out var entity, out _, out var meta, out var trans))
        {
            if (meta.EntityPrototype?.ID != targetSpawnId)
                continue;

            if (trans.GridUid != arena)
                continue;

            QueueDel(entity);
            return trans.Coordinates;
        }

        return Transform(arena).Coordinates;
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
        _mapsystem.CreateMap(out var mapId);
        var opts = DeserializationOptions.Default with { InitializeMaps = true };
        if (!_map.TryLoadGrid(mapId, new ResPath(arenaPath), out var grid, opts))
        {
            return null;
        }

        return grid;
    }
}
