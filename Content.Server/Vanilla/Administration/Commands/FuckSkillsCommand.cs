using Content.Server.Administration;
using Content.Server.Administration.Logs;
using Content.Shared.Administration;
using Content.Server.Audio;
using Content.Shared.Database;
using Content.Shared.Vanilla.Skill;
using Robust.Shared.Console;
using Robust.Shared.IoC;
using Robust.Shared.Player;
using Robust.Shared.GameObjects;


namespace Content.Server.vanilla.Administration.Commands;

[AdminCommand(AdminFlags.Admin)]
public sealed class FuckSkillsCommand : IConsoleCommand
{
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly EntityManager _entityManager = default!;

    public string Command => "fuckskills";
    public string Description => "выдать всем полные навыки";
    public string Help => "fuckskills";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        // Получаем все сущности под управлением игроков
        var query = _entityManager.EntityQueryEnumerator<ActorComponent>();

        var playerFilter = Filter.Empty();

        while (query.MoveNext(out var uid, out var actor))
        {
            if (!_entityManager.TryGetComponent(uid, out SkillComponent? skillComp))
                skillComp = _entityManager.AddComponent<SkillComponent>(uid);

            // Присваиваем максимальные уровни всем навыкам
            skillComp.PilotingLevel = 3;
            skillComp.RangeWeaponLevel = 3;
            skillComp.MeleeWeaponLevel = 3;
            skillComp.MedicineLevel = 3;
            skillComp.ChemistryLevel = 3;
            skillComp.EngineeringLevel = 3;
            skillComp.BuildingLevel = 3;
            skillComp.ResearchLevel = 3;
            skillComp.InstrumentationLevel = 3;

            _entityManager.Dirty(skillComp);

            playerFilter.AddPlayer(actor.PlayerSession);
        }
        //звуковое сопровождение
        _entityManager.System<ServerGlobalSoundSystem>().PlayAdminGlobal(playerFilter, "/Audio/Vanilla/SkillSystem/levelup.ogg");


        _adminLogger.Add(
            LogType.AdminMessage,
            LogImpact.Extreme,
            $"Admin {(shell.Player != null ? shell.Player.Name : "An administrator")} gave max skills to all entities under player control.");
        
        shell.WriteLine("Все навыки выданы всем сущностям под управлением игроков.");
    }

}
