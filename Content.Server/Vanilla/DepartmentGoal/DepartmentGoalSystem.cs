using Content.Server.Fax;
using Content.Server.GameTicking.Events;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Server.Corvax.StationGoal;
using Content.Server.Cargo.Components;
using Content.Server.Cargo.Systems;
using Content.Server.Chat.Systems;
using Content.Shared.Station.Components;
using Content.Shared.Fax.Components;
using Content.Shared.GameTicking;
using Content.Shared.Paper;
using Content.Shared.Research.Components;
using Content.Shared.Cargo.Prototypes;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using System.Linq;

namespace Content.Server.Vanilla.DepartmentGoal;
/// <summary>
///     System to spawn paper with station goal.
/// </summary>
public sealed class DepartmentGoalSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly FaxSystem _fax = default!;
    [Dependency] private readonly CargoSystem _cargo = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly ChatSystem _chatSystem = default!;
    public Dictionary<EntityUid, List<DepartmentGoalPrototype>> _depGoals = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundStartedEvent>(OnRoundStarting);
    }
    #region отправка целей
    private void OnRoundStarting(RoundStartedEvent ev)
    {
        _depGoals.Clear();

        // Получаем все прототипы DepartmentGoalPrototype (цели)
        var allGoals = _proto.EnumeratePrototypes<DepartmentGoalPrototype>().ToList();

        // Группируем цели по отделам
        var goalsByDepartment = allGoals.GroupBy(g => g.Department);

        // Перемешиваем порядок отделов
        goalsByDepartment = goalsByDepartment.OrderBy(_ => _random.Next()).ToList();

        // Перебираем все станции с компонентом StationGoalComponent
        var query = EntityQueryEnumerator<StationDataComponent, StationGoalComponent>();
        while (query.MoveNext(out var stationUid, out _, out _))
        {
            // Локальный список для хранения выбранных целей для текущей станции
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

            // Добавляем цели для текущей станции в словарь
            _depGoals[stationUid] = departmentGoals;

            // Отправляем выбранные цели для текущей станции
            foreach (var departmentGoal in departmentGoals)
            {
                SendStationGoal(stationUid, departmentGoal);
            }
        }
    }

    public void SendStationGoal(EntityUid ent, DepartmentGoalPrototype goal)
    {
        // Создание факса с текстом цели
        var printout = new FaxPrintout(
            Loc.GetString(goal.Text, ("station", MetaData(ent).EntityName), ("dep", goal.Department.ToString())),
            Loc.GetString("station-goal-fax-paper-name"),
            null,
            null,
            "paper_stamp-centcom",
            new List<StampDisplayInfo>
            {
                new() { StampedName = Loc.GetString("stamp-component-stamped-name-centcom"), StampedColor = Color.FromHex("#006600") },
            });

        // Перебираем все факс-устройства в мире
        var query = EntityQueryEnumerator<FaxMachineComponent>();
        while (query.MoveNext(out var faxUid, out var fax))
        {
            // Если факс не поддерживает прием целей, пропускаем
            if (!fax.ReceiveStationGoal)
                continue;

            // Получаем наибольшую сетку для станции и проверяем, на ней ли факс
            var largestGrid = _station.GetLargestGrid(ent);
            var grid = Transform(faxUid).GridUid;

            if (grid is not null && largestGrid == grid.Value)
            {
                // Отправляем факс с данными
                _fax.Receive(faxUid, printout, null, fax);
            }
        }
    }
    #endregion
    #region принятие целей
    public bool ApproveGoal(DepartmentGoalPrototype goal)
    {
        // Находим станцию, к которой привязана эта цель
        var station = _depGoals.FirstOrDefault(x => x.Value.Contains(goal)).Key;

        if (station == default)
        {
            Logger.Error($"Не найдена станция для цели: {goal.ID}");
            return false;
        }

        int randomValue = _random.Next(15000, 35000);
        ProtoId<CargoAccountPrototype> account = goal.Department switch
        {
            department.RnD => "Science",
            department.MED => "Medical",
            department.CARGO => "Cargo",
            department.ENG => "Engineering",
            department.SEC => "Security",
            department.SRV => "Service",
            _ => "Cargo"
        };

        _cargo.UpdateBankAccount(station, randomValue, account);
        DispatchAnnouncement(goal.Department, randomValue);
        return true;
    }

    private void DispatchAnnouncement(department dep, int randomValue)
    {
        _chatSystem.DispatchGlobalAnnouncement(Loc.GetString("Department-goal-text", ("dep", dep.ToString()), ("rand", randomValue)),
                                                Loc.GetString("Department-goal-title"));
    }
#endregion

}
