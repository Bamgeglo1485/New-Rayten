using System.Linq;
using Content.Server.Fax;
using Content.Server.GameTicking.Events;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Server.Corvax.StationGoal;
using Content.Server.Cargo.Components;
using Content.Server.Cargo.Systems;
using Content.Server.Station.Components;
using Content.Shared.Fax.Components;
using Content.Shared.GameTicking;
using Content.Shared.Paper;
using Content.Shared.Research.Components;
using Content.Server.Chat.Systems;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Vanilla.DepartmentGoal;
/// <summary>
///     System to spawn paper with station goal.
/// </summary>
public sealed class DepartmentGoalSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly FaxSystem _fax = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly CargoSystem _cargoSystem = default!;
    [Dependency] private readonly ChatSystem _chatSystem = default!;
    public List<DepartmentGoalPrototype> _depGoals = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundStartingEvent>(OnRoundStarting);
    }
    #region отправка целей
    private void OnRoundStarting(RoundStartingEvent ev)
    {
        // Очистка старых данных
        _depGoals.Clear();

        // Получаем все прототипы DepartmentGoalPrototype (цели)
        var allGoals = _proto.EnumeratePrototypes<DepartmentGoalPrototype>().ToList();

        // Группируем цели по отделам
        var goalsByDepartment = allGoals.GroupBy(g => g.Department);
        // Перемешиваем порядок отделов
        goalsByDepartment = goalsByDepartment.OrderBy(_ => _random.Next()).ToList();

        // Локальный список для хранения выбранных целей
        var departmentGoals = new List<DepartmentGoalPrototype>();

        // Перебираем каждый отдел
        foreach (var departmentGroup in goalsByDepartment)
        {
            var department = departmentGroup.Key;

            // Перемешиваем цели внутри отдела
            var departmentGoalsList = departmentGroup
                .OrderBy(_ => _random.Next())
                .ToList();

            // Суммарный вес целей в отделе
            var totalWeight = departmentGoalsList.Sum(g => g.Weight);
            if (totalWeight <= 0)
            {
                continue;
            }

            // Генерируем случайное число от 0 до totalWeight
            var randomValue = _random.NextFloat(0, totalWeight);
            float cumulativeWeight = 0;
            // Выбираем цель случайно с учётом веса
            foreach (var goal in departmentGoalsList)
            {
                cumulativeWeight += goal.Weight;
                if (randomValue <= cumulativeWeight)
                {
                    departmentGoals.Add(goal);
                    break;
                }
            }

            if (departmentGoals.Count == 4)
                break;
        }

        // Сохраняем наши цели
        _depGoals = departmentGoals;

        // Перебираем все станции с компонентом StationGoalComponent
        var query = EntityQueryEnumerator<StationGoalComponent>();
        while (query.MoveNext(out var stationUid, out var station))
        {
            // Отправляем выбранные цели
            foreach (var departmentGoal in departmentGoals)
            {
                SendStationGoal(stationUid, departmentGoal);
            }
        }
    }

    public bool SendStationGoal(EntityUid? ent, ProtoId<DepartmentGoalPrototype> goal)
    {
        return SendStationGoal(ent, _proto.Index(goal));
    }

    /// <summary>
    ///     Send a station goal on selected station to all faxes which are authorized to receive it.
    /// </summary>
    /// <returns>True if at least one fax received paper</returns>
    public bool SendStationGoal(EntityUid? ent, DepartmentGoalPrototype goal)
    {
        // Если передана пустая сущность, не отправляем цель
        if (ent is null)
            return false;

        // Проверяем наличие компонента данных о станции
        if (!TryComp<StationDataComponent>(ent, out var stationData))
            return false;

        // Создание факса с текстом цели
        var printout = new FaxPrintout(
            Loc.GetString(goal.Text, ("station", MetaData(ent.Value).EntityName), ("dep", goal.Department.ToString())),
            Loc.GetString("station-goal-fax-paper-name"),
            null,
            null,
            "paper_stamp-centcom",
            new List<StampDisplayInfo>
            {
                new() { StampedName = Loc.GetString("stamp-component-stamped-name-centcom"), StampedColor = Color.FromHex("#006600") },
            });

        bool wasSent = false;

        // Перебираем все факс-устройства в мире
        var query = EntityQueryEnumerator<FaxMachineComponent>();
        while (query.MoveNext(out var faxUid, out var fax))
        {
            // Если факс не поддерживает прием целей, пропускаем
            if (!fax.ReceiveStationGoal)
            {
                continue;
            }

            // Получаем наибольшую сетку для станции и проверяем, на ней ли факс
            var largestGrid = _station.GetLargestGrid(stationData);
            var grid = Transform(faxUid).GridUid;

            if (grid is not null && largestGrid == grid.Value)
            {
                // Отправляем факс с данными
                _fax.Receive(faxUid, printout, null, fax);

                wasSent = true;
            }
        }

        return wasSent;
    }
    #endregion
    #region принятие целей
    public bool ApproveGoal(DepartmentGoalPrototype goal)
    {
        bool gived = false;
        bool itemBenefit = false;
        bool researchBenefit = false;
        bool allBenefit = false;

        // награда в виде предметов
        if (goal.ItemBenefits != null && goal.ItemBenefits.Count > 0)
        {
            foreach (var itemId in goal.ItemBenefits)
            {
                HandleItemBenefit(itemId);
            }
            itemBenefit = true;
            gived = true;
        }

        // награда в виде очков изучения
        if(goal.ResearchBenefit > 0 )
        {
            HandleResearchBenefit(goal.ResearchBenefit);
            gived = true;
            researchBenefit = true;
        }
        
        allBenefit = itemBenefit && researchBenefit;
            
        float randomValue = (int)_random.NextFloat(0, 100);
        DispatchAnnouncement(goal.Department, itemBenefit, researchBenefit, allBenefit, randomValue);
        return gived; 
    }

    private void DispatchAnnouncement(department dep, bool itemBenefits, bool researcBenefits, bool allBenefit, float randomValue)
    {
        if(allBenefit)
        {
            _chatSystem.DispatchGlobalAnnouncement(Loc.GetString("Department-goal-text-all", ("dep", dep.ToString()), ("rand", randomValue)),
                                                   Loc.GetString("Department-goal-title"));
            return;
        }
        if(itemBenefits)
        {
            _chatSystem.DispatchGlobalAnnouncement(Loc.GetString("Department-goal-text-item", ("dep", dep.ToString()), ("rand", randomValue)),
                                                   Loc.GetString("Department-goal-title"));
            return;
        }
        if(researcBenefits)
        {
            _chatSystem.DispatchGlobalAnnouncement(Loc.GetString("Department-goal-text-research", ("dep", dep.ToString()), ("rand", randomValue)),
                                                   Loc.GetString("Department-goal-title"));
            return;
        }
    }
    private void HandleResearchBenefit(int count)
    {
        // Перебираем все сущности с компонентом ResearchServerComponent
        var query = EntityQueryEnumerator<ResearchServerComponent>();
        while (query.MoveNext(out var entityUid, out var researchServer))
        {
            // Добавляем очки к Points
            researchServer.Points += count;
            Dirty(entityUid,researchServer);
        }
    }


    private void HandleItemBenefit(EntProtoId item)
    {
        // Перебираем все станции с компонентом StationGoalComponent
        var query = EntityQueryEnumerator<StationGoalComponent>();
        while (query.MoveNext(out var stationUid, out var station))
        {
            if (TryComp<StationCargoOrderDatabaseComponent>(stationUid, out var cargoDb) && 
                TryComp<StationDataComponent>(stationUid, out var stationData))
            {
                var product = _proto.Index<EntityPrototype>(item);
                _cargoSystem.AddAndApproveOrder(
                    stationUid, 
                    product.ID, 
                    product.Name, 
                    0, // Стоимость
                    1, // количество
                    "NanoTrasen", 
                    "Награда за выполнение цели", 
                    "Торговый департамент", 
                    cargoDb,
                    (stationUid, stationData)
                );
            }

        }
    }


#endregion

}