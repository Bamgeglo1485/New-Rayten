using Content.Server.Administration;
using Content.Server.Administration.Logs;
using Content.Server.Vanilla.DepartmentGoal;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;
using System.Linq;

namespace Content.Server.Vanilla.Administration.Commands;

[AdminCommand(AdminFlags.Admin)]
public sealed class ApproveGoalCommand : IConsoleCommand
{
    [Dependency] private readonly IEntitySystemManager _entitySystemManager = default!;

    public string Command => "approveGoal";
    public string Description => "Принять цель как выполненную и выдать награду";
    public string Help => "Используйте: uprovegoal <ID цели>";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteLine("Укажите ID цели.");
            return;
        }

        var goalId = args[0];
        var departmentGoalSystem = _entitySystemManager.GetEntitySystem<DepartmentGoalSystem>();

        // Ищем цель во всех станциях
        DepartmentGoalPrototype? foundGoal = null;
        EntityUid stationUid = default;
        foreach (var (station, goals) in departmentGoalSystem.DepGoals)
        {
            var goal = goals.FirstOrDefault(g => g.ID == goalId);
            if (goal != null)
            {
                foundGoal = goal;
                stationUid = station;
                break;
            }
        }

        if (foundGoal == null)
        {
            shell.WriteLine($"Цель с ID '{goalId}' не найдена.");
            return;
        }

        // Выполняем цель!
        if (departmentGoalSystem.ApproveGoal(foundGoal))
        {
            shell.WriteLine($"Цель '{foundGoal.ID}' выполнена! Слава NT!");
        }
        else
        {
            shell.WriteLine($"не получилось :c");
        }

        // Удаляем цель из списка станции
        if (departmentGoalSystem.DepGoals.TryGetValue(stationUid, out var goalsList))
        {
            goalsList.Remove(foundGoal);

            // Если у станции больше нет целей, можно удалить её из словаря
            if (goalsList.Count == 0)
            {
                departmentGoalSystem.DepGoals.Remove(stationUid);
            }
        }
    }
    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
        {
            var departmentGoalSystem = _entitySystemManager.GetEntitySystem<DepartmentGoalSystem>();

            var goalIds = departmentGoalSystem.DepGoals
                .SelectMany(kv => kv.Value)  // Выбираем все списки целей
                .Select(g => g.ID)           // Берём ID каждой цели
                .Distinct()                  // Убираем дубликаты (если нужно)
                .ToArray();

            return CompletionResult.FromHintOptions(goalIds, "ID доступных целей");
        }

        return CompletionResult.Empty;
    }
}
