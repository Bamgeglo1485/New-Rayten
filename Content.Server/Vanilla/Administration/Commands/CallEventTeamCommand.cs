using Content.Server.Administration;
using Content.Server.Administration.Logs;
using Content.Shared.Administration;
using Content.Shared.Database;
using Robust.Server.GameObjects;
using Robust.Shared.Console;
using Robust.Shared.IoC;
using Robust.Shared.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Content.Server.Vanilla.EventTeam;
using System.Linq;

namespace Content.Server.vanilla.Administration.Commands;

[AdminCommand(AdminFlags.Admin)]
public sealed class CallEventTeamCommand : IConsoleCommand
{
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly IEntitySystemManager _entitySystemManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    public string Command => "calleventteam";
    public string Description => "Вызвать отряд на станцию!";
    public string Help => "calleventteam";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var eventTeamSystem = _entitySystemManager.GetEntitySystem<EventTeamSystem>();
        
        if (args.Length < 1 || args.Length > 2)
        {
            shell.WriteLine("Укажите id отряда");
            return;
        }
        var EventTeamId = args[0];
        if (!_prototypes.TryIndex<EventTeamPrototype>(EventTeamId, out _))
        {
            shell.WriteLine($"отряда с ID {EventTeamId} не существует.");
            return;
        }

        bool ignoreJammer = false;
        if (args.Length == 2)
        {
            if (!bool.TryParse(args[1], out ignoreJammer))
            {
                shell.WriteLine("Неверное значение для игнорирования глушилки. Должно быть true или false.");
                return;
            }
        }

        eventTeamSystem.call(EventTeamId, ignoreJammer);

        _adminLogger.Add(
            LogType.AdminMessage,
            LogImpact.Extreme,
            $"Admin {(shell.Player != null ? shell.Player.Name : "An administrator")} called an Event Team !");
        
        shell.WriteLine($"{EventTeamId} был вызван!");
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
        {
            var eventTeamIds = _prototypes.EnumeratePrototypes<EventTeamPrototype>()
                                        .Select(p => p.ID)
                                        .ToArray();
            return CompletionResult.FromHintOptions(eventTeamIds, "ID доступных отрядов");
        }

        if (args.Length == 2)
        {
            return CompletionResult.FromHintOptions(new[] { "true", "false" }, "Игнорировать глушилку");
        }

        return CompletionResult.Empty;
    }

}
