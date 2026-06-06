using Content.Server.Administration;
using Content.Server.Administration.Logs;
using Content.Shared.Administration;
using Content.Shared.Database;
using Content.Shared.Vanilla.Skill;
using Robust.Shared.Console;
using Robust.Shared.IoC;
using Robust.Shared.Player;
using Robust.Shared.GameObjects;


namespace Content.Server.vanilla.Administration.Commands;

[AdminCommand(AdminFlags.Admin)]
public sealed partial class FuckSkillsCommand : IConsoleCommand
{
    [Dependency] private IAdminLogManager _adminLogger = default!;
    [Dependency] private EntityManager _entityManager = default!;
    private SharedSkillSystem? _skill;

    public string Command => "fuckskills";
    public string Description => "выдать всем полные навыки";
    public string Help => "fuckskills";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        _skill = _entityManager.System<SharedSkillSystem>();
        var query = _entityManager.EntityQueryEnumerator<ActorComponent, SkillComponent>();
        while (query.MoveNext(out var uid, out _, out var skillcomp))
            _skill.FuckSkills(uid, skillcomp);

        _adminLogger.Add(
            LogType.AdminMessage,
            LogImpact.Extreme,
            $"Admin {(shell.Player != null ? shell.Player.Name : "An administrator")} gave max skills to all entities under player control.");

        shell.WriteLine("Все навыки выданы всем сущностям под управлением игроков.");
    }

}
