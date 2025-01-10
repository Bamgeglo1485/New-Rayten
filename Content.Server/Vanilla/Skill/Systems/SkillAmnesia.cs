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

    const int _experienceToRestore = 1;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<SkillAmnesiaComponent, ComponentRemove>(OnComponentRemove);
    }

    private void OnMobStateChanged(MobStateChangedEvent ev)
    {
        // Проверяем наличие компонента навыков
        if (!TryComp<SkillComponent>(ev.Target, out var skill))
            return;

        // Если память защищена, выходим
        if(HasComp<MemoryShieldComponent>(ev.Target))
            return;

        // Проверяем компоненты актора и амнезии
        TryComp<ActorComponent>(ev.Target, out var actor);
        TryComp<SkillAmnesiaComponent>(ev.Target, out var skillAmnesia);

        // Обработка смерти сущности
        if (ev.NewMobState == MobState.Dead)
        {
            // Если есть амнезия, учитываем её
            if (skillAmnesia!=null)
            {
                var timeSinceDeath = _gameTiming.RealTime - skillAmnesia.TimeOfDeath;

                LoseExperience(skill, skillAmnesia.skilltype);
                skill.Dirty();

                // Удаляем амнезию только если прошло более 3 минут
                if (timeSinceDeath.TotalMinutes >= 3)
                    EntityManager.RemoveComponent<SkillAmnesiaComponent>(ev.Target);
            }
            else
            {
                // Удаляем весь опыт, если амнезии нет
                LoseExperience(skill);
                skill.Dirty();
            }

            // Обновляем навыки клиента
            if (actor != null)
                RaiseNetworkEvent(new UpdateCharacterSkillsRequestEvent(), Filter.SinglePlayer(actor.PlayerSession));

            return; // Завершаем, так как смерть обработана
        }

        // если чел воскрешается и у него нет амнезии - даём её
        if (ev.OldMobState == MobState.Dead && skillAmnesia == null)
        {
            SkillAmnesia(ev.Target, skill);
            skill.Dirty();

            // Обновляем навыки клиента
            if (actor != null)
                RaiseNetworkEvent(new UpdateCharacterSkillsRequestEvent(), Filter.SinglePlayer(actor.PlayerSession));
        }
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

        var nonZeroSkills = skillLevels.Where(s => s.Level > 0).ToList();
        if (nonZeroSkills.Count == 0)
            return;

        var selectedSkill = _random.Pick(nonZeroSkills);

        if (selectedSkill.Level == 1)
        {
            selectedSkill.SetLevel(0);
            return;
        }

        SkillAmnesiaComponent amnesia = EntityManager.AddComponent<SkillAmnesiaComponent>(user);
        amnesia.skilltype = Enum.Parse<skillType>(selectedSkill.Name);

        int newLevel = selectedSkill.Level == 2 ? 0 : selectedSkill.Level - 1;
        selectedSkill.SetLevel(newLevel);

        amnesia.TimeOfDeath = _gameTiming.CurTime;
        StartRemember(user, skill, amnesia);
    }


    //метод тупа запускает таймер
    private void StartRemember(EntityUid user, SkillComponent skill, SkillAmnesiaComponent amnesia)
    {
        amnesia.TokenSource?.Cancel();
        amnesia.TokenSource = new CancellationTokenSource();

        // Запуск таймера, который будет срабатывать каждую секунду
        user.SpawnRepeatingTimer(TimeSpan.FromSeconds(1), () => Remember(skill, amnesia.skilltype, amnesia, user), amnesia.TokenSource.Token);
    }

    //таймер перекачивает опыт из SkillAmnesiaComponent в SkillComponent
    private void Remember(SkillComponent skill, skillType skillType, SkillAmnesiaComponent amnesia, EntityUid user)
    {
        amnesia.exptorestore -= _experienceToRestore;
        
        amnesia.Dirty();

        TryComp<ActorComponent>(user, out var actor);
        if(_serverSkillTrainerSystem.AddExperience(skill, skillType, _experienceToRestore))
            if(actor != null) _audio.PlayGlobal("/Audio/Vanilla/SkillSystem/levelup.ogg", actor.PlayerSession);

        if (actor != null) RaiseNetworkEvent(new UpdateCharacterSkillsRequestEvent(), Filter.SinglePlayer(actor.PlayerSession));

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
    private void LoseExperience(SkillComponent skill, skillType? ignoredSkill = null)
    {
        skill.SkillPoints = 0;

        if (ignoredSkill != skillType.Piloting) skill.PilotingExp = 0;
        if (ignoredSkill != skillType.RangeWeapon) skill.RangeWeaponExp = 0;
        if (ignoredSkill != skillType.MeleeWeapon) skill.MeleeWeaponExp = 0;
        if (ignoredSkill != skillType.Medicine) skill.MedicineExp = 0;
        if (ignoredSkill != skillType.Chemistry) skill.ChemistryExp = 0;
        if (ignoredSkill != skillType.Engineering) skill.EngineeringExp = 0;
        if (ignoredSkill != skillType.Building) skill.BuildingExp = 0;
        if (ignoredSkill != skillType.Research) skill.ResearchExp = 0;
        if (ignoredSkill != skillType.Instrumentation) skill.InstrumentationExp = 0;
    }
}