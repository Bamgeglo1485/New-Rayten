using Content.Server.Administration;
using Content.Server.Shuttles.Systems;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Content.Server.Vanilla.Evacuation;

namespace Content.Server.Vanilla.Evacuation.Commands;
[AdminCommand(AdminFlags.Fun)]
sealed partial class StartEvacuationCommand : IConsoleCommand
{
    [Dependency] private IEntityManager _entityManager = default!;

    public string Command => "evacuation";
    public string Description => "Начинает процедуру эвакуации с заданным временем";
    public string Help => "evacuation <время>";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var evac = _entityManager.System<EvacuationSystem>();

        if (args.Length != 1)
        {
            evac.StartEvacuation();
        }
        else
        {
            if (!float.TryParse(args[0], out var amount))
                shell.WriteLine(Loc.GetString("Значение должно быть числом", ("arg", args[0])));
            evac.StartEvacuation(amount);
        }
    }
}
