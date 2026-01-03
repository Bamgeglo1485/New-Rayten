using Content.Shared.Mobs;
using Content.Shared.Vanilla.MemoryShield;
using Robust.Shared.Random;
using System.Linq;

namespace Content.Shared.Vanilla.Skill;

public abstract partial class SharedSkillSystem : EntitySystem
{

    const int ExpPerSecond = 2; //такое количество опыта восстанавливается каждую секунду
    public override void Update(float frameTime)
    {
        var curTime = _timing.CurTime;

        var query = EntityQueryEnumerator<SkillAmnesiaComponent, SkillComponent>();
        while (query.MoveNext(out var entity, out var amnesia, out var skillComp))
        {
            // Ждем 1 секунду перед следующим обновлением
            if (curTime < amnesia.NextUpdateTime)
                continue;

            // Обновляем таймер
            amnesia.NextUpdateTime = curTime + TimeSpan.FromSeconds(1);

            // Перекачка опыта
            Remember(entity, skillComp, amnesia);
        }
    }

    private void OnAmnesiaInit(EntityUid uid, SkillAmnesiaComponent component, MapInitEvent args)
    {
        if (!TryComp<SkillComponent>(uid, out var skill))
        {
            RemComp<SkillAmnesiaComponent>(uid);
            return;
        }

        var amnesiableSkills = new List<SkillType>();

        // Basic
        foreach (var (skillType, level) in skill.BasicSkills)
            if (level > SkillLevel.None)
                amnesiableSkills.Add(skillType);

        // Easy
        foreach (var skillType in skill.EasySkills)
            amnesiableSkills.Add(skillType);

        if (amnesiableSkills.Count == 0)
        {
            RemComp<SkillAmnesiaComponent>(uid);
            return;
        }

        var selectedSkill = _Random.Pick(amnesiableSkills);

        if (!TryGetSkill(uid, selectedSkill, out var easySkillLevel, out var basicSkillLevel, skill))
            return;

        switch (selectedSkill.GetKind())
        {
            case SkillKind.Easy:
                component.Exptorestore = EXPERIENCETONEWLVL;
                skill.EasySkills.Remove(selectedSkill);
                break;

            case SkillKind.Basic:
                component.Exptorestore = (int)basicSkillLevel * EXPERIENCETONEWLVL;
                skill.BasicSkills[selectedSkill] = basicSkillLevel - 1;
                break;
        }
        component.Skilltype = selectedSkill;
        component.TimeOfDeath = _timing.CurTime;

        Dirty(uid, component);
        Dirty(uid, skill);
    }

    private void OnMobStateChanged(EntityUid uid, SkillComponent component, MobStateChangedEvent ev)
    {
        if (HasComp<MemoryShieldComponent>(uid))
            return;
        TryComp<SkillAmnesiaComponent>(uid, out var skillAmnesia);

        //Если сдохли в момент отката амнезии - тереям навык окончательно
        if (ev.NewMobState == MobState.Dead)
        {
            if (skillAmnesia != null)
            {
                var timeSinceDeath = _timing.CurTime - skillAmnesia.TimeOfDeath;
                if (timeSinceDeath.TotalMinutes >= 2)
                {
                    LoseExperience(component, skillAmnesia.Skilltype);
                    RemComp<SkillAmnesiaComponent>(uid);
                }
            }
        }
        //Вышли из состояния смерти - дарим амнезию
        if (ev.OldMobState == MobState.Dead && skillAmnesia == null)
            EnsureComp<SkillAmnesiaComponent>(uid);
    }

    private void Remember(EntityUid user, SkillComponent skill, SkillAmnesiaComponent amnesia)
    {
        if (amnesia.Exptorestore <= 0)
        {
            RemComp<SkillAmnesiaComponent>(user);
            return;
        }
        amnesia.Exptorestore -= ExpPerSecond;
        AddExperience((user, skill), amnesia.Skilltype, ExpPerSecond);
        Dirty(user, amnesia);
    }


    private void LoseExperience(SkillComponent skill, SkillType? ignoredSkill = null)
    {
        skill.SkillPoints = 0;

        foreach (var skillType in skill.SkillExps.Keys.ToArray())
        {
            if (skillType == ignoredSkill)
                continue;

            skill.SkillExps[skillType] = 0;
        }
    }

}