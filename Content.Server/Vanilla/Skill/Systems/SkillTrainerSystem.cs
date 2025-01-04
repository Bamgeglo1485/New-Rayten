using Content.Shared.Interaction.Events;
using Content.Shared.Vanilla.Skill;
using Content.Shared.DoAfter;
using Robust.Shared.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Content.Shared.SkillTrainer;
using Content.Shared.Mobs.Components;
using Content.Shared.Ghost;
using Content.Shared.Popups;
using Content.Shared.Vanilla.Skill;
using Content.Shared.Examine;

namespace Content.Server.SkillTrainer;

public sealed class ServerSkillTrainerSystem : EntitySystem
{
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] protected readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SkillTrainerComponent, UseInHandEvent>(OnUserInHand);
        SubscribeLocalEvent<SkillTrainerComponent, TrainEvent>(HandleTrainEvent);
        SubscribeLocalEvent<SkillTrainerComponent, ExaminedEvent>(OnExamine);
        SubscribeNetworkEvent<RequestSkillAddEXPEvent>(onSkillAddEXPEvent);
    }

    private void OnExamine(EntityUid uid, SkillTrainerComponent component, ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;
        
        if(!EntityManager.TryGetComponent<SkillComponent>(args.Examiner, out var skillComp))
            skillComp = EnsureComp<SkillComponent>(args.Examiner);

        int SkillExpToLearn = skillComp.GetSkillExpToLearn(component.SkillType);
        args.PushMarkup(Loc.GetString("examine-skilltrainer-part-1", ("skilltype", component.SkillType.ToString())));
        args.PushMarkup(Loc.GetString("examine-skilltrainer-part-2", ("SkillExpToLearn", SkillExpToLearn)));
    }

    private void onSkillAddEXPEvent(RequestSkillAddEXPEvent msg, EntitySessionEventArgs args)
    {
        // Проверяем, что у пользователя есть прикрепленное существо, и что навык задан
        if (!args.SenderSession.AttachedEntity.HasValue || msg.skill == null)
            return;

        var entity = args.SenderSession.AttachedEntity.Value;

        // Получаем компонент навыков
        if (!EntityManager.TryGetComponent<SkillComponent>(entity, out var skillComp) || skillComp.SkillPoints < 1)
            return;

        // Проверяем уровень навыка
        int skillLevel = skillComp.GetSkillLevel(msg.skill);
        
        // Если уровень навыка уже максимальный, прекращаем выполнение
        if (skillLevel >= 3)
            return;

        // Уменьшаем очки навыков
        skillComp.SkillPoints--;
        skillComp.Dirty();

        // Добавляем опыт
        if (AddExperience(skillComp, msg.skill, 100))
            _audio.PlayGlobal("/Audio/Vanilla/SkillSystem/levelup.ogg", args.SenderSession);

        RaiseNetworkEvent(new UpdateCharacterSkillsRequestEvent(), Filter.SinglePlayer(args.SenderSession));
    }

    private void OnUserInHand(EntityUid uid, SkillTrainerComponent component, UseInHandEvent args)
    {
        // Проверка на валидность пользователя
        if (!HasComp<MobStateComponent>(args.User) || HasComp<GhostComponent>(args.User))
            return;

        if (!EntityManager.TryGetComponent<SkillComponent>(args.User, out var skillComp))
            skillComp = EnsureComp<SkillComponent>(args.User);

        if (skillComp.GetSkillExpToLearn(component.SkillType) <= 0)
        {
            var overtrainKey = $"Skill-train-overtrain-{component.SkillType.ToString().ToLower()}";
            _popup.PopupEntity(Loc.GetString(overtrainKey), args.User, args.User);
            return;
        }

        // Обработка действия
        if (!args.Handled)
        {
            StartDoAfter(args.User, component, uid);
            args.Handled = true;
        }
    }


    private void StartDoAfter(EntityUid user, SkillTrainerComponent component, EntityUid uid)
    {
        _audio.PlayPvs("/Audio/Vanilla/SkillSystem/bookpaperswish.ogg", user, AudioParams.Default.WithMaxDistance(2f));
        var doAfterArgs = new DoAfterArgs(EntityManager, user, TimeSpan.FromSeconds(component.ReadTime), new TrainEvent
        {
            SkillType = component.SkillType,
            SkillIncreaseAmount = component.SkillIncreaseAmount
        }, eventTarget: uid)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            NeedHand = true,
        };
        _doAfter.TryStartDoAfter(doAfterArgs);
    }

    private void HandleTrainEvent(EntityUid uid, SkillTrainerComponent component, TrainEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;
            
        if (!EntityManager.TryGetComponent<SkillComponent>(args.User, out var skillComp))
            skillComp = EnsureComp<SkillComponent>(args.User);

        if(TryComp<ActorComponent>(args.User, out var actor))
        {

            int exp = DecreaseSkillExpToLearn(skillComp, args.SkillType, args.SkillIncreaseAmount);    

            if(AddExperience(skillComp, args.SkillType, exp))
                _audio.PlayGlobal("/Audio/Vanilla/SkillSystem/levelup.ogg", actor.PlayerSession);
            else
                StartDoAfter(args.User, component, uid);

            RaiseNetworkEvent(new UpdateCharacterSkillsRequestEvent(), Filter.SinglePlayer(actor.PlayerSession));
        }
        args.Handled = true;
    }

    public bool AddExperience(SkillComponent skillComp, skillType skillType, int experienceAmount)
    {
        // Получаем уровень и опыт для переданного навыка
        int level = skillComp.GetSkillLevel(skillType);
        int exp = skillComp.GetSkillExp(skillType);

        // Проверка ограничения уровня
        if (level >= 3) return false;

        // Расчёт необходимого опыта
        int requiredExp = 300 + level * 300;
        exp += experienceAmount;
        // Проверка на повышение уровня
        if (exp >= requiredExp)
        {
            // Увеличиваем уровень и перераспределяем опыт
            SetSkillLevel(skillComp, skillType, level + 1);
            SetSkillExp(skillComp, skillType, exp - requiredExp);
            return true;
        }
        SetSkillExp(skillComp, skillType, exp);
        return false;
    }

    private void SetSkillLevel(SkillComponent skillComp, skillType skill, int level)
    {
        switch (skill)
        {
            case skillType.Piloting:
                skillComp.PilotingLevel = level;
                break;
            case skillType.RangeWeapon:
                skillComp.RangeWeaponLevel = level;
                break;
            case skillType.MeleeWeapon:
                skillComp.MeleeWeaponLevel = level;
                break;
            case skillType.Medicine:
                skillComp.MedicineLevel = level;
                break;
            case skillType.Chemistry:
                skillComp.ChemistryLevel = level;
                break;
            case skillType.Engineering:
                skillComp.EngineeringLevel = level;
                break;
            case skillType.Building:
                skillComp.BuildingLevel = level;
                break;
            case skillType.Research:
                skillComp.ResearchLevel = level;
                break;
            case skillType.Instrumentation:
                skillComp.InstrumentationLevel = level;
                break;
            default:
                break;
        }
        skillComp.Dirty();
    }
    private int DecreaseSkillExpToLearn(SkillComponent skillComp, skillType skill, int exp)
    {
        if (exp < 0) return 0;

        switch (skill)
        {
            case skillType.Piloting:

                if(exp > skillComp.PilotingExpToLearn)
                    exp = skillComp.PilotingExpToLearn;
                skillComp.PilotingExpToLearn -= exp;

                break;
            case skillType.RangeWeapon:
                if(exp > skillComp.RangeWeaponExpToLearn)
                    exp = skillComp.RangeWeaponExpToLearn;
                skillComp.RangeWeaponExpToLearn -= exp;
                break;
            case skillType.MeleeWeapon:
                if(exp > skillComp.MeleeWeaponExpToLearn)
                    exp = skillComp.MeleeWeaponExpToLearn;
                skillComp.MeleeWeaponExpToLearn -= exp;
                break;
            case skillType.Medicine:
                if(exp > skillComp.MedicineExpToLearn)
                    exp = skillComp.MedicineExpToLearn;
                skillComp.MedicineExpToLearn -= exp;
                break;
            case skillType.Chemistry:
                if(exp > skillComp.ChemistryExpToLearn)
                    exp = skillComp.ChemistryExpToLearn;
                skillComp.ChemistryExpToLearn -= exp;
                break;
            case skillType.Engineering:
                if(exp > skillComp.EngineeringExpToLearn)
                    exp = skillComp.EngineeringExpToLearn;
                skillComp.EngineeringExpToLearn -= exp;
                break;
            case skillType.Building:
                if(exp > skillComp.BuildingExpToLearn)
                    exp = skillComp.BuildingExpToLearn;
                skillComp.BuildingExpToLearn -= exp;
                break;
            case skillType.Research:
                if(exp > skillComp.ResearchExpToLearn)
                    exp = skillComp.ResearchExpToLearn;
                skillComp.ResearchExpToLearn -= exp;
                break;
            case skillType.Instrumentation:
                if(exp > skillComp.InstrumentationExpToLearn)
                    exp = skillComp.InstrumentationExpToLearn;
                skillComp.InstrumentationExpToLearn -= exp;
                break;
            default:
                break;
        }
        skillComp.Dirty();
        return exp;
    }
    private void SetSkillExp(SkillComponent skillComp, skillType skill, int exp)
    {
        if (exp < 0) return;

        switch (skill)
        {
            case skillType.Piloting:
                skillComp.PilotingExp = exp;
                break;
            case skillType.RangeWeapon:
                skillComp.RangeWeaponExp = exp;
                break;
            case skillType.MeleeWeapon:
                skillComp.MeleeWeaponExp = exp;
                break;
            case skillType.Medicine:
                skillComp.MedicineExp = exp;
                break;
            case skillType.Chemistry:
                skillComp.ChemistryExp = exp;
                break;
            case skillType.Engineering:
                skillComp.EngineeringExp = exp;
                break;
            case skillType.Building:
                skillComp.BuildingExp = exp;
                break;
            case skillType.Research:
                skillComp.ResearchExp = exp;
                break;
            case skillType.Instrumentation:
                skillComp.InstrumentationExp = exp;
                break;
            default:
                break;
        }
        skillComp.Dirty();
    }
}
