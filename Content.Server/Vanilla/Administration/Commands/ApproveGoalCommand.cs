using Content.Server.Administration;
using Content.Server.Administration.Logs;
using Content.Server.Vanilla.DepartmentGoal;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.IoC;
using Robust.Shared.Prototypes;
using System.Linq;

namespace Content.Server.Vanilla.Administration.Commands;

[AdminCommand(AdminFlags.Admin)]
public sealed class ApproveGoalCommand : IConsoleCommand
{
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly IEntitySystemManager _entitySystemManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

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

        // Проверяем, существует ли цель с указанным ID
        var goal = departmentGoalSystem._depGoals.FirstOrDefault(g => g.ID == goalId);
        if (goal == null)
        {
            shell.WriteLine($"Цель с ID '{goalId}' не найдена.");
            return;
        }

        // Помечаем цель как выполненную (здесь можно добавить логику наград)
        if(departmentGoalSystem.ApproveGoal(goal))
        {
            shell.WriteLine($"Цель '{goal.ID}' выполнена! Слава NT!");
            
        }
        else
        {
            shell.WriteLine($"у цели '{goal.ID}' нет награды");
        }
        //удаляем цель из списка
        departmentGoalSystem._depGoals.Remove(goal);
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
        {
            var departmentGoalSystem = _entitySystemManager.GetEntitySystem<DepartmentGoalSystem>();
            var goalIds = departmentGoalSystem._depGoals.Select(g => g.ID).ToArray();

            return CompletionResult.FromHintOptions(goalIds, "ID доступных целей");
        }

        return CompletionResult.Empty;
    }
}
