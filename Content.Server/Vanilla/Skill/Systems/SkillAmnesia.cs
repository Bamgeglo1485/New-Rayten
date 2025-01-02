using System.Linq;
using System.Threading;
using Content.Server.Vanilla.MemoryShield;
using Content.Shared.Mobs;
using Content.Shared.Vanilla.Skill;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Content.Server.SkillTrainer;

namespace Content.Server.Vanilla.Skill;

public sealed class SkillAmnesiaSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly ServerSkillTrainerSystem _serverSkillTrainerSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<SkillAmnesiaComponent, ComponentRemove>(OnComponentRemove);
    }

    //точка входа в систему амнезии - срабатывает при смене состояние человека
    private void OnMobStateChanged(MobStateChangedEvent ev)
    {
        // навыки теряются только если есть что терять
        if (!TryComp<SkillComponent>(ev.Target, out var skill))
            return;

        // Если память защищена - ничего не делаем
        if (TryComp<MemoryShieldComponent>(ev.Target, out var memshield))
            return;

        //Если сущность умерла - теряет очки навыков и опыт
        if(ev.NewMobState == MobState.Dead)
            LoseExperience(skill);

        // амнезия начинается только при воскрешени
        if (ev.OldMobState != MobState.Dead)
            return;

        // навыки теряются только если прошло 3 минуты с момента предыдущей смерти
        if (TryComp<SkillAmnesiaComponent>(ev.Target, out var SkillWithAmnesia))
        {
            var timeSinceDeath = _gameTiming.RealTime.Subtract(SkillWithAmnesia.TimeOfDeath);

            if(timeSinceDeath.Minutes < 3)
                return;

            EntityManager.RemoveComponent<SkillAmnesiaComponent>(ev.Target);
        }


        SkillAmnesia(ev.Target, skill);
        skill.Dirty();
        if(TryComp<ActorComponent>(ev.Target, out var actor))
            RaiseNetworkEvent(new UpdateCharacterSkillsRequestEvent(), Filter.SinglePlayer(actor.PlayerSession));
    }


    /*
    Метод выбирает рандомный навык из компонента skill и затем уменьшает его уровень, затем создаёт компонент amnesia и задаёт начальные значения в зависимости от забытого уровня.
    */
    private void SkillAmnesia(EntityUid user, SkillComponent skill)
    {
        var skillLevels = new List<(string Name, int Level, Action<int> SetLevel)>
        {
            ("Piloting", skill.PilotingLevel, level => skill.PilotingLevel = level),
            ("RangeWeapon", skill.RangeWeaponLevel, level => skill.RangeWeaponLevel = level),
            ("MeleeWeapon", skill.MeleeWeaponLevel, level => skill.MeleeWeaponLevel = level),
            ("Medicine", skill.MedicineLevel, level => skill.MedicineLevel = level),
            ("Chemistry", skill.ChemistryLevel, level => skill.ChemistryLevel = level),
            ("Engineering", skill.EngineeringLevel, level => skill.EngineeringLevel = level),
            ("Building", skill.BuildingLevel, level => skill.BuildingLevel = level),
            ("Research", skill.ResearchLevel, level => skill.ResearchLevel = level),
            ("Instrumentation", skill.InstrumentationLevel, level => skill.InstrumentationLevel = level)
        };
        // Оставляем только навыки с уровнем выше 0. Если навыков нет, ничего не делаем
        var nonZeroSkills = skillLevels.Where(s => s.Level > 0).ToList();
        if (nonZeroSkills.Count == 0)
            return;

        // Выбираем случайный навык
        var selectedSkill = _random.Pick(nonZeroSkills);

        // Добавляем компонент SkillAmnesiaComponent
        SkillAmnesiaComponent amnesia = EntityManager.AddComponent<SkillAmnesiaComponent>(user);

        // Сохраняем тип выбранного навыка
        amnesia.skilltype = Enum.Parse<skillType>(selectedSkill.Name);

        // Потерянный опыт в зависимости от уровня навыка
        amnesia.exptorestore = selectedSkill.Level == 1 ? 300 : 900;
        int newLevel = selectedSkill.Level == 2 ? selectedSkill.Level - 2 : selectedSkill.Level - 1 ;

        // Забываем уровень
        switch (selectedSkill.Name)
        {
            case "Piloting":
                skill.PilotingLevel = newLevel;
                break;
            case "RangeWeapon":
                skill.RangeWeaponLevel = newLevel;
                break;
            case "MeleeWeapon":
                skill.MeleeWeaponLevel = newLevel;
                break;
            case "Medicine":
                skill.MedicineLevel = newLevel;
                break;
            case "Chemistry":
                skill.ChemistryLevel = newLevel;
                break;
            case "Engineering":
                skill.EngineeringLevel = newLevel;
                break;
            case "Building":
                skill.BuildingLevel = newLevel;
                break;
            case "Research":
                skill.ResearchLevel = newLevel;
                break;
            case "Instrumentation":
                skill.InstrumentationLevel = newLevel;
                break;
            default:
                break;
        }
        amnesia.TimeOfDeath = _gameTiming.CurTime;
        StartRemember(user, skill, amnesia); //стартуем вспоминание навыка
    }

    //метод тупа запускает таймер
    private void StartRemember(EntityUid user, SkillComponent skill, SkillAmnesiaComponent amnesia)
    {
        amnesia.TokenSource?.Cancel();
        amnesia.TokenSource = new CancellationTokenSource();

        // Запуск таймера, который будет срабатывать каждые 2 секунды
        user.SpawnRepeatingTimer(TimeSpan.FromSeconds(2), () => Remember(skill, amnesia.skilltype, amnesia, user), amnesia.TokenSource.Token);
    }

    //таймер перекачивает опыт из SkillAmnesiaComponent в SkillComponent
    private void Remember(SkillComponent skill, skillType skillType, SkillAmnesiaComponent amnesia, EntityUid user)
    {
        int experienceToRestore = (amnesia.exptorestore >= 3) ? 3 : amnesia.exptorestore;
        amnesia.exptorestore -= experienceToRestore;
        
        amnesia.Dirty();
        
        if(TryComp<ActorComponent>(user, out var actor))
        {
            if(_serverSkillTrainerSystem.AddExperience(skill, skillType, experienceToRestore))
                _audio.PlayGlobal("/Audio/Vanilla/SkillSystem/levelup.ogg", actor.PlayerSession);
            RaiseNetworkEvent(new UpdateCharacterSkillsRequestEvent(), Filter.SinglePlayer(actor.PlayerSession));
        }
        if (amnesia.exptorestore <= 0)
            EntityManager.RemoveComponent<SkillAmnesiaComponent>(user);
    }

    /*
        Удаляем таймеры при удалении компонента
    */
    private void OnComponentRemove(EntityUid uid, SkillAmnesiaComponent amnesia, ComponentRemove args)
    {
        amnesia.TokenSource?.Cancel();
    }


    /*
    Метод отвечает за сброс всего опыта и скиллпоинтов при смерти. Ну и всё.
    */
    private void LoseExperience(SkillComponent skill)
    {
        skill.SkillPoints = 0;
        skill.PilotingExp = 0;
        skill.RangeWeaponExp = 0;
        skill.MeleeWeaponExp = 0;
        skill.MedicineExp = 0;
        skill.ChemistryExp = 0;
        skill.EngineeringExp = 0;
        skill.BuildingExp = 0;
        skill.ResearchExp = 0;
        skill.InstrumentationExp = 0;
    }
}