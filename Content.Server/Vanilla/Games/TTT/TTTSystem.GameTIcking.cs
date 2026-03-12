using Content.Shared.Vanilla.Games.TTT;
using Content.Shared.Mobs;
using Robust.Shared.Timing;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Content.Shared.Chat;
using Content.Server.Respawn;
using Content.Shared.Mind;
using Content.Shared.Clothing;
using Content.Server.Chat.Managers;
using Content.Server.Destructible;
using System.Linq;
using Robust.Shared.Utility;
using Robust.Shared.EntitySerialization;
using Robust.Shared.Audio.Systems;
using Content.Shared.Implants;
using Content.Shared.Hands.EntitySystems;
using Robust.Shared.EntitySerialization.Systems;
using Content.Shared.Strip.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Physics;
using Content.Shared.HealthExaminable;
namespace Content.Server.Vanilla.Games.TTT;

public sealed partial class TTTSystem : SharedTTTSystem
{
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly SpecialRespawnSystem _specialRespawn = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly SharedMindSystem _mindSystem = default!;
    [Dependency] private readonly LoadoutSystem _loadout = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedSubdermalImplantSystem _implant = default!;
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    private sealed class PlayerStats(string name, int kills, int karma, string role)
    {
        public string Name { get; set; } = name;
        public int Kills { get; set; } = kills;
        public int Karma { get; set; } = karma;
        public string Role { get; set; } = role;
    }
    private void OnMobStateChanged(EntityUid uid, TTTMarkerComponent component, MobStateChangedEvent args)
    {
        if (args.OldMobState != MobState.Alive)
            return;

        if (!TryComp<TTTRuleComponent>(component.RuleLink, out var rule))
            return;

        if (TryComp<TTTMarkerComponent>(args.Origin, out var sourcecomp))
        {
            if (IsSameTeam(sourcecomp.Role, component.Role))
                sourcecomp.TotalKills--;
            else if (args.Origin.Value != uid)
                sourcecomp.TotalKills++;
        }

        if (component.Role == TTTRole.Traitor)
            rule.TraitorsCount--;
        else
            rule.InoCount--;
        rule.TimeToNewCycle += rule.HasteAddTime;

        RemComp<StrippableComponent>(uid);
        RemComp<NameOverlayComponent>(uid);
        if (TryComp<FixturesComponent>(uid, out var fixtures))
        {
            var fixture = fixtures.Fixtures.First();
            _physics.SetDensity(uid, fixture.Key, fixture.Value, 1f);
        }
        var body = EnsureComp<TTTDeadBodyComponent>(uid);
        body.DeathTime = _gameTiming.CurTime;
        body.Killer = args.Origin;
        body.RuleLink = component.RuleLink;
        if (args.Origin != null)
            body.Gun = _hands.GetActiveItem(args.Origin.Value);

        if (rule.InoCount <= 0 || rule.TraitorsCount <= 0)
            GameOver(rule);
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
            if (rule.CurrentStatus == TTTStatus.AwaitStart)
            {
                //Количество игроков меньше 4 не трогаем ваще
                if (rule.Sessions.Count < 3)
                    rule.TimeForPlayersJoin = TimeSpan.FromSeconds(30f);
                else if (rule.TimeForPlayersJoin > TimeSpan.FromSeconds(0))
                    rule.TimeForPlayersJoin -= TimeSpan.FromSeconds(1); //обратный отсчёт
                else
                    rule.CurrentStatus = TTTStatus.Startup; //время на сбор заявок вышло, спавним игроков
            }

            //Начали раунд, спавним всех кто пожелал поучаствовать
            if (rule.CurrentStatus == TTTStatus.Startup)
                MakeStartUp(uid, rule);

            if (rule.CurrentStatus > TTTStatus.AwaitStart)
                rule.TimeOnNewCycle += TimeSpan.FromSeconds(1);

            //обратный отсчёт
            if (rule.CurrentStatus == TTTStatus.AwaitRolesToAdd && rule.TimeOnNewCycle > TimeSpan.FromSeconds(30))
                GiveRoles(rule);

            // Пора запустить новый цикл
            if (rule.CurrentStatus == TTTStatus.RoundInProgress)
                RoundProcess(rule);
        }
    }
    /// <summary>
    /// Этап спавна карты, игроков итд итп
    /// </summary>
    private void MakeStartUp(EntityUid uid, TTTRuleComponent rule)
    {
        //Сообщаем о том что всё конец сбора заявок парни
        UpdateInformation(rule);
        if (rule.Sessions.Count < 3 || !TrySpawnArena(rule.Sessions.Count, rule))
        {
            GameOver(rule);
            return;
        }

        //проходим по всем игрокам и закидываем их на арену
        foreach (var session in rule.Sessions)
            AddPlayerToArena(session, rule, uid);

        _audio.PlayGlobal(rule.AwaitRolesMusic, Filter.Empty().AddPlayers(rule.Sessions), false);
        rule.CurrentStatus = TTTStatus.AwaitRolesToAdd; //Вот теперь матч реально начался
    }

    //Метод добавляет игрока на арену
    public EntityUid AddPlayerToArena(ICommonSession session, TTTRuleComponent rule, EntityUid ruleEnt)
    {
        _specialRespawn.TryFindRandomTile(rule.Arena, _mapSystem.GetMap(rule.ArenaMapId), 10, out var targetCoords);

        var mobUid = Spawn("TTTMob", targetCoords);
        _meta.SetEntityName(mobUid, session.Name);

        if (_mindSystem.TryGetMind(session.AttachedEntity!.Value, out var mindId, out var mindComp))
            _mindSystem.TransferTo(mindId, mobUid, true, mind: mindComp);

        //Добавляем метки
        RemComp<HealthExaminableComponent>(mobUid);
        RemComp<DestructibleComponent>(mobUid);
        var marker = EnsureComp<TTTMarkerComponent>(mobUid);
        marker.RuleLink = ruleEnt;
        marker.Session = session;
        var nameMarker = EnsureComp<NameOverlayComponent>(mobUid);
        nameMarker.Name = session.Name;

        _loadout.Equip(mobUid, rule.StartingGear, null);
        var message = Loc.GetString("ttt-awaitrole-brief");
        var wrappedMessage = Loc.GetString("chat-manager-server-wrap-message", ("message", message));
        _chatManager.ChatMessageToOne(ChatChannel.Server, message, wrappedMessage, default, false, session.Channel);
        return mobUid;
    }
    /// <summary>
    /// раздача ролей
    /// </summary>
    private void GiveRoles(TTTRuleComponent rule)
    {
        var traitorsCount = GetTraitorCount(rule.Sessions.Count);
        var deccount = GetDecCount(rule.Sessions.Count);
        rule.CurrentStatus = TTTStatus.RoundInProgress;

        var query = EntityQueryEnumerator<TTTMarkerComponent>();
        while (query.MoveNext(out var uid, out var marker))
        {
            if (!rule.Sessions.Contains(marker.Session))
                continue;
            string message;
            //делаем предателем
            if (traitorsCount > 0)
            {
                traitorsCount--;
                marker.Role = TTTRole.Traitor;
                rule.TraitorsCount++;

                _implant.AddImplant(uid, "TraitorShopImplant");
                EnsureComp<TTTTRAITORComponent>(uid);
                EnsureComp<ShowTTTTraitorsComponent>(uid);

                _audio.PlayGlobal(rule.TraitorBrief, uid);
                message = Loc.GetString("ttt-traitor-brief", ("color", Color.Red));
            }
            //делаем детективом
            else if (deccount > 0)
            {
                deccount--;
                marker.Role = TTTRole.Detective;
                rule.InoCount++;

                _implant.AddImplant(uid, "DetectiveShopImplant");
                var nameMarker = EnsureComp<NameOverlayComponent>(uid);
                nameMarker.NameColor = Color.DodgerBlue;
                Dirty(uid, nameMarker);
                var dnaScanner = Spawn("TTTDNAScanner", Transform(uid).Coordinates);
                _hands.TryPickupAnyHand(uid, dnaScanner);

                _audio.PlayGlobal(rule.DecBrief, uid);
                message = Loc.GetString("ttt-Detective-brief", ("color", Color.DodgerBlue));
            }
            //делаем обычником
            else
            {
                marker.Role = TTTRole.Inocent;
                rule.InoCount++;
                _audio.PlayGlobal(rule.InoBrief, uid);
                message = Loc.GetString("ttt-innocent-brief", ("color", Color.Green));
            }
            Dirty(uid, marker);
            var wrappedMessage = Loc.GetString("chat-manager-server-wrap-message", ("message", message));
            _chatManager.ChatMessageToOne(ChatChannel.Server, message, wrappedMessage, default, false, marker.Session.Channel);
        }
    }
    /// <summary>
    /// управляем уже идущим раундом
    /// </summary>
    private void RoundProcess(TTTRuleComponent rule)
    {
        var timetoend = rule.TimeToNewCycle - rule.TimeOnNewCycle;

        if (timetoend < TimeSpan.FromMinutes(5) && rule.Announcments == 0)
        {
            DispatchMonospaceAnnouncement(Filter.Empty().AddPlayers(rule.Sessions), Loc.GetString("ttt-timetoend-5"), Color.Green);
            rule.Announcments++;
        }
        if (timetoend < TimeSpan.FromMinutes(3) && rule.Announcments == 1)
        {
            DispatchMonospaceAnnouncement(Filter.Empty().AddPlayers(rule.Sessions), Loc.GetString("ttt-timetoend-3"), Color.Yellow);
            rule.Announcments++;
        }
        if (timetoend < TimeSpan.FromMinutes(1) && rule.Announcments == 2)
        {
            DispatchMonospaceAnnouncement(Filter.Empty().AddPlayers(rule.Sessions), Loc.GetString("ttt-timetoend-1"), Color.Red);
            rule.Announcments++;
        }
        if (rule.TimeOnNewCycle >= rule.TimeToNewCycle)
            GameOver(rule);
    }
    /// <summary>
    /// Завершает пизделку, сообщает кто победил, запускает новый цикл
    /// </summary>
    private void GameOver(TTTRuleComponent rule)
    {
        rule.CurrentStatus = TTTStatus.Ended;

        var winner = rule.InoCount <= 0; // 1 - предатели, 0 - невиновные

        //послераундовый бриф
        List<PlayerStats> statsList = [];
        var traitorSessions = new List<ICommonSession>();
        var innocentSessions = new List<ICommonSession>();
        var result = $"{"Игрок".PadRight(16)}| {"Роль".PadRight(10)}| {"Убийств".PadRight(7)}| {"Карма".PadRight(5)}\n";
        var query = EntityQueryEnumerator<TTTMarkerComponent>();
        while (query.MoveNext(out var uid, out var marker))
        {
            if (!rule.Sessions.Contains(marker.Session))
                continue;

            //музыка
            if (marker.Role == TTTRole.Traitor)
                traitorSessions.Add(marker.Session);
            if (marker.Role == TTTRole.Inocent || marker.Role == TTTRole.Detective)
                innocentSessions.Add(marker.Session);
            //карма
            AddKarma(marker.Session.UserId, KarmaRoundIncrement);
            if (!marker.TeamKiller)
                AddKarma(marker.Session.UserId, KarmaCleanBonus);

            statsList.Add(new PlayerStats(marker.Session.Name, marker.TotalKills, (int)GetKarma(marker.Session.UserId), marker.GetRoleName()));
        }
        var sorted = statsList
            .OrderByDescending(s => s.Role)
            .ThenByDescending(s => s.Karma)
            .ToList();

        foreach (var stat in sorted)
        {
            var name = stat.Name[..Math.Min(stat.Name.Length, 16)];
            result += name.PadRight(16) + "| " + stat.Role.PadRight(10) + "| " + stat.Kills.ToString().PadRight(7) + "| " + stat.Karma.ToString().PadRight(5) + "\n";
        }

        var message = Loc.GetString("ttt-gameover", ("winner", winner), ("result", result));
        DispatchMonospaceAnnouncement(Filter.Empty().AddPlayers(rule.Sessions), message, winner ? Color.Red : Color.Green);

        var traitorFilter = Filter.Empty().AddPlayers(traitorSessions);
        var innocentFilter = Filter.Empty().AddPlayers(innocentSessions);

        _audio.PlayGlobal(winner ? rule.WinSound : rule.LoseSound, traitorFilter, false);
        _audio.PlayGlobal(winner ? rule.LoseSound : rule.WinSound, innocentFilter, false);
        SaveKarma();
        NewCycle(rule);
    }

    //обновляем все значения
    public void NewCycle(TTTRuleComponent rule)
    {
        Timer.Spawn(TimeSpan.FromSeconds(1), () => QueueDel(rule.Arena));
        rule.Sessions = []; //Сбрасываем предыдущих пользователей
        rule.CurrentStatus = TTTStatus.AwaitStart; //начинаем собирать игроков в раунд
        rule.TimeForPlayersJoin = TimeSpan.FromSeconds(30f);
        rule.TimeOnNewCycle = TimeSpan.FromSeconds(0);
        rule.TimeToNewCycle = TimeSpan.FromMinutes(5);
        rule.Announcments = 0;
        rule.InoCount = 0;
        rule.TraitorsCount = 0;
        UpdateInformation(rule);
    }
    private bool TrySpawnArena(int playerCount, TTTRuleComponent rule)
    {
        if (!_mapSystem.MapExists(rule.ArenaMapId))
        {
            _mapSystem.CreateMap(out var newMapId);
            rule.ArenaMapId = newMapId;
        }

        // Пытаемся загрузить грид на карту
        if (!_mapLoader.TryLoadGrid(rule.ArenaMapId, new ResPath(_random.Pick(rule.Arenas)), out var grid, DeserializationOptions.Default))
            return false;

        rule.Arena = grid.Value;
        return true;
    }
    private static int GetTraitorCount(int playerCount)
    {
        return Math.Max(1, playerCount / 4);
    }

    private static int GetDecCount(int playerCount)
    {
        if (playerCount < 8)
            return 0;

        return Math.Max(1, playerCount / 8);
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
