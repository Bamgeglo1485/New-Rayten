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
            skillComp.PilotingLevel = SkillLevel.Expert;
            skillComp.RangeWeaponLevel = SkillLevel.Expert;
            skillComp.MeleeWeaponLevel = SkillLevel.Expert;
            skillComp.MedicineLevel = SkillLevel.Expert;
            skillComp.ChemistryLevel = SkillLevel.Expert;
            skillComp.EngineeringLevel = SkillLevel.Expert;
            skillComp.BuildingLevel = SkillLevel.Expert;
            skillComp.ResearchLevel = SkillLevel.Expert;
            skillComp.InstrumentationLevel = SkillLevel.Expert;

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
