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
    }

    public override void Update(float frameTime)
    {
        var curTime = _gameTiming.CurTime;

        var query = EntityQueryEnumerator<SkillAmnesiaComponent, SkillComponent>();
        while (query.MoveNext(out var entity, out var amnesia, out var skillComp))
        {
            // Ждем 1 секунду перед следующим обновлением
            if (curTime < amnesia.NextUpdateTime)
                continue;

            // Обновляем таймер
            amnesia.NextUpdateTime = curTime + TimeSpan.FromSeconds(1);

            // Перекачка опыта
            Remember(skillComp, amnesia.skilltype, amnesia, entity);
        }
    }

    private void Remember(SkillComponent skill, skillType skillType, SkillAmnesiaComponent amnesia, EntityUid user)
    {
        amnesia.exptorestore -= _experienceToRestore;

        Dirty(user, amnesia);
        TryComp<ActorComponent>(user, out var actor);

        if (amnesia.exptorestore <= 0)
            EntityManager.RemoveComponent<SkillAmnesiaComponent>(user);

        _serverSkillTrainerSystem.AddExperience(skill, skillType, _experienceToRestore, multiplyed: false, player: actor?.PlayerSession);
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
                Dirty(ev.Target, skill);


                // Удаляем амнезию только если прошло более 2 минут
                if (timeSinceDeath.TotalMinutes >= 2)
                    EntityManager.RemoveComponent<SkillAmnesiaComponent>(ev.Target);
            }
            else
            {
                // Удаляем весь опыт, если амнезии нет
                LoseExperience(skill);
                Dirty(ev.Target, skill);
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
            Dirty(ev.Target, skill);

            // Обновляем навыки клиента
            if (actor != null)
                RaiseNetworkEvent(new UpdateCharacterSkillsRequestEvent(), Filter.SinglePlayer(actor.PlayerSession));
        }
    }

    private bool inspectamensiableskill(SkillComponent skillcomp, skillType Skilltype)
    {
        var level = skillcomp.GetSkillLevel(Skilltype);

        if (level == null)
            return skillcomp.GetEasySkill(Skilltype) ?? false;

        return (int)level > 0;
    }

    /*
    Метод выбирает рандомный навык из компонента skill и затем уменьшает его уровень, затем создаёт компонент amnesia и задаёт начальные значения в зависимости от забытого уровня.
    */
    private void SkillAmnesia(EntityUid user, SkillComponent skill)
    {
        var skillLevels = new List<(skillType skill, bool amnesiable, Action DecreaseLvL)>
        {
            //Основные навыки
            (skillType.RangeWeapon, inspectamensiableskill(skill, skillType.RangeWeapon), () => skill.RangeWeaponLevel--),
            (skillType.MeleeWeapon, inspectamensiableskill(skill, skillType.MeleeWeapon), () => skill.MeleeWeaponLevel--),
            (skillType.Medicine, inspectamensiableskill(skill, skillType.Medicine), () => skill.MedicineLevel--),
            (skillType.Chemistry, inspectamensiableskill(skill, skillType.Chemistry), () => skill.ChemistryLevel--),
            (skillType.Engineering, inspectamensiableskill(skill, skillType.Engineering), () => skill.EngineeringLevel--),
            (skillType.Building, inspectamensiableskill(skill, skillType.Building), () => skill.BuildingLevel--),
            (skillType.Research, inspectamensiableskill(skill, skillType.Research), () => skill.ResearchLevel--),
            (skillType.Instrumentation, inspectamensiableskill(skill, skillType.Instrumentation), () => skill.InstrumentationLevel--),
            //Лёгкие навыки
            (skillType.Piloting, inspectamensiableskill(skill, skillType.Piloting), () => skill.Piloting = false),
            (skillType.MusInstruments, inspectamensiableskill(skill, skillType.MusInstruments), () => skill.MusInstruments = false),
            (skillType.Botany, inspectamensiableskill(skill, skillType.Botany), () => skill.Botany = false),
            (skillType.Bureaucracy, inspectamensiableskill(skill, skillType.Bureaucracy), () => skill.Bureaucracy = false)
        };
    
        var nonZeroSkills = skillLevels.Where(s => s.amnesiable == true).ToList();
        if (nonZeroSkills.Count == 0)
            return;

        var selectedSkill = _random.Pick(nonZeroSkills);

        SkillAmnesiaComponent amnesia = EntityManager.AddComponent<SkillAmnesiaComponent>(user);
        amnesia.skilltype = selectedSkill.skill;

        selectedSkill.DecreaseLvL();

        amnesia.TimeOfDeath = _gameTiming.CurTime;
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
        if (ignoredSkill != skillType.Botany) skill.BotanyExp = 0;
        if (ignoredSkill != skillType.MusInstruments) skill.MusInstrumentsExp = 0;
        if (ignoredSkill != skillType.Bureaucracy) skill.BureaucracyExp = 0;
    }
}