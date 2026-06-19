using System.Linq;
using Content.Server.Administration;
using Content.Shared.Administration;
using Content.Shared.Prototypes;
using Content.Shared.Vanilla.CompoundZ;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;

namespace Content.Server.Vanilla.CompoundZ.Commands;

[AdminCommand(AdminFlags.Debug)]
public sealed partial class MakeSuperCommand : IConsoleCommand
{
    [Dependency] private IEntityManager _entityManager = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;

    public string Command => "makesuper";
    public string Description => "Даёт указанному объекту суперсилу";
    public string Help => "makesuper <uid> <SuperAbilityId>";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 2)
        {
            shell.WriteError("makesuper <uid> <SuperAbilityId>");
            return;
        }

        if (!NetEntity.TryParse(args[0], out var targetUidNet))
        {
            shell.WriteError($"Инвалид UID: {args[0]}");
            return;
        }

        if (!_entityManager.TryGetEntity(targetUidNet, out var targetEntity))
        {
            shell.WriteError($"Энтити с UID {args[0]} не найдена");
            return;
        }

        if (!_entityManager.EntityExists(targetEntity.Value))
        {
            shell.WriteError($"Энтити с UID {args[0]} не найдена");
            return;
        }

        if (!_prototypeManager.TryIndex<SuperAbilityPrototype>(args[1], out var superPrototype))
        {
            shell.WriteError($"Способность '{args[1]}' не найдена");

            var available = _prototypeManager.EnumeratePrototypes<SuperAbilityPrototype>()
                .Select(p => p.ID)
                .Order();
            shell.WriteLine("Список способностей:");
            foreach (var id in available)
            {
                shell.WriteLine($"  - {id}");
            }
            return;
        }

        var superSystem = _entityManager.System<SharedSuperSystem>();
        superSystem.GrantSuperAbility(targetEntity.Value, args[1]);

        shell.WriteLine($"Суперспособность '{args[1]}' успешно добавлена к энтити {targetEntity.Value}");
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
        {
            return CompletionResult.FromHintOptions(
                new[] { "<uid>" },
                "Entity UID");
        }

        if (args.Length == 2)
        {
            var superPrototypes = _prototypeManager.EnumeratePrototypes<SuperAbilityPrototype>()
                .Select(p => p.ID)
                .Order();

            return CompletionResult.FromHintOptions(
                superPrototypes,
                "Super Ability ID");
        }

        return CompletionResult.Empty;
    }
}
