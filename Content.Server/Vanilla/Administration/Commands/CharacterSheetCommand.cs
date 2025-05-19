
using Content.Server.Vanilla.Background.eui;
using Content.Server.Administration;
using Content.Server.EUI;
using Content.Shared.Administration;
using Robust.Server.Player;
using Robust.Shared.Console;
using Robust.Shared.Player;
using Robust.Shared.Network;

namespace Content.Server.Vanilla.Administration.Commands;

[AdminCommand(AdminFlags.Admin)]
public sealed class CharacterSheetCommand : LocalizedCommands
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly EuiManager _euiManager = default!;

    public override string Command => "charactersheet";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1 || !EntityUid.TryParse(args[0], out var entityUid))
        {
            shell.WriteError("Неверные аргументы. Использование: charactersheet <entityUid>");
            return;
        }

        if (shell.Player == null)
        {
            shell.WriteError("Команда должна вызываться от игрока.");
            return;
        }

        var netEntity = _entManager.GetNetEntity(entityUid);
        var eui = new CharacterSheetEui(netEntity);
        _euiManager.OpenEui(eui, shell.Player);

        shell.WriteLine($"Открыт лист персонажа для сущности {entityUid}.");
    }
}
