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
        SubscribeNetworkEvent<RequestSkillAddEXPEvent>(onSkillAddEXPEvent);

    }
    private void onSkillAddEXPEvent(RequestSkillAddEXPEvent msg, EntitySessionEventArgs args)
    {
        if (!args.SenderSession.AttachedEntity.HasValue||msg.skill==null)
            return;
        var entity = args.SenderSession.AttachedEntity.Value;

        if (!EntityManager.TryGetComponent<SkillComponent>(entity, out var skillComp) || skillComp.SkillPoints<1)
            return;
        switch (msg.skill)
            {
                case "Chemistry":
                    if(skillComp.ChemistryLevel>=3)
                        return;
                    break;
                case "Medicine":
                    if(skillComp.MedicineLevel>=3)
                        return;
                    break;
                case "RangeWeapon":
                    if(skillComp.RangeWeaponLevel>=3)
                        return;
                    break;
                case "Piloting":
                    if(skillComp.PilotingLevel>=3)
                        return;
                    break;
                case "Research":
                    if(skillComp.ResearchLevel>=3)
                        return;
                    break;
                case "Instrumentation":
                    if(skillComp.InstrumentationLevel>=3)
                        return;
                    break;
                case "Building":
                    if(skillComp.BuildingLevel>=3)
                        return;
                    break;
                case "Engineering":
                    if(skillComp.EngineeringLevel>=3)
                        return;
                    break;
            }
        skillComp.SkillPoints--;
        skillComp.Dirty();

        if(AddExperience(skillComp, msg.skill, 100, 3))
            _audio.PlayPvs("/Audio/Vanilla/SkillSystem/levelup.ogg", entity, AudioParams.Default.WithMaxDistance(1f));
        RaiseNetworkEvent(new UpdateCharacterSkillsRequestEvent(), Filter.SinglePlayer(args.SenderSession));
    }

    private void OnUserInHand(EntityUid uid, SkillTrainerComponent component, UseInHandEvent args)
    {
        if (!HasComp<MobStateComponent>(args.User) || HasComp<GhostComponent>(args.User))
        return;
        if(EntityManager.TryGetComponent<SkillComponent>(args.User, out var skillComp)){
            switch (component.SkillType)
            {
                case "Chemistry":
                    if(skillComp.ChemistryLevel>=component.MaxLevel){
                        _popup.PopupEntity(Loc.GetString("Skill-train-overtrain-chemistry"), args.User, args.User);
                        return;
                    }
                    break;
                case "Medicine":
                    if(skillComp.MedicineLevel>=component.MaxLevel){
                        _popup.PopupEntity(Loc.GetString("Skill-train-overtrain-medicine"), args.User, args.User);
                        return;
                    }
                    break;
                case "RangeWeapon":
                    if(skillComp.RangeWeaponLevel>=component.MaxLevel){
                        _popup.PopupEntity(Loc.GetString("Skill-train-overtrain-rangeweapon"), args.User, args.User);
                        return;
                    }
                    break;
                case "Piloting":
                    if(skillComp.PilotingLevel>=component.MaxLevel){
                        _popup.PopupEntity(Loc.GetString("Skill-train-overtrain-piloting"), args.User, args.User);
                        return;
                    }
                    break;
                case "Research":
                    if(skillComp.ResearchLevel>=component.MaxLevel){
                        _popup.PopupEntity(Loc.GetString("Skill-train-overtrain-research"), args.User, args.User);
                        return;
                    }
                    break;
                case "Instrumentation":
                    if(skillComp.InstrumentationLevel>=component.MaxLevel){
                        _popup.PopupEntity(Loc.GetString("Skill-train-overtrain-instrumentation"), args.User, args.User);
                        return;
                    }
                    break;
                case "Building":
                    if(skillComp.BuildingLevel>=component.MaxLevel){
                        _popup.PopupEntity(Loc.GetString("Skill-train-overtrain-Building"), args.User, args.User);
                        return;
                    }
                    break;
                case "Engineering":
                    if(skillComp.EngineeringLevel>=component.MaxLevel){
                        _popup.PopupEntity(Loc.GetString("Skill-train-overtrain-engineering"), args.User, args.User);
                        return;
                    }
                    break;
            }
        }
        else
            skillComp = EnsureComp<SkillComponent>(args.User);
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
            MaxLevel = component.MaxLevel,
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
        if(AddExperience(skillComp, args.SkillType, args.SkillIncreaseAmount, args.MaxLevel))
            _audio.PlayPvs("/Audio/Vanilla/SkillSystem/levelup.ogg", args.User, AudioParams.Default.WithMaxDistance(3f));
        else
            StartDoAfter(args.User, component, uid);

        if (TryComp<ActorComponent>(args.User, out var actor))
            RaiseNetworkEvent(new UpdateCharacterSkillsRequestEvent(), Filter.SinglePlayer(actor.PlayerSession));

        args.Handled = true;
    }
    public bool AddExperience(SkillComponent skillComp, string skillType, int experienceAmount, int maxLevel)
    {

    var skills = new Dictionary<string, (Func<int> GetLevel, Action<int> SetLevel, Func<int> GetExp, Action<int> SetExp)>
    {
        ["Chemistry"] = (() => skillComp.ChemistryLevel, val => skillComp.ChemistryLevel = val,
                        () => skillComp.ChemistryExp, val => skillComp.ChemistryExp = val),
        ["Medicine"] = (() => skillComp.MedicineLevel, val => skillComp.MedicineLevel = val,
                        () => skillComp.MedicineExp, val => skillComp.MedicineExp = val),
        ["RangeWeapon"] = (() => skillComp.RangeWeaponLevel, val => skillComp.RangeWeaponLevel = val,
                        () => skillComp.RangeWeaponExp, val => skillComp.RangeWeaponExp = val),
        ["Piloting"] = (() => skillComp.PilotingLevel, val => skillComp.PilotingLevel = val,
                        () => skillComp.PilotingExp, val => skillComp.PilotingExp = val),
        ["Research"] = (() => skillComp.ResearchLevel, val => skillComp.ResearchLevel = val,
                        () => skillComp.ResearchExp, val => skillComp.ResearchExp = val),
        ["Instrumentation"] = (() => skillComp.InstrumentationLevel, val => skillComp.InstrumentationLevel = val,
                            () => skillComp.InstrumentationExp, val => skillComp.InstrumentationExp = val),
        ["Engineering"] = (() => skillComp.EngineeringLevel, val => skillComp.EngineeringLevel = val,
                            () => skillComp.EngineeringExp, val => skillComp.EngineeringExp = val),
        ["Building"] = (() => skillComp.BuildingLevel, val => skillComp.BuildingLevel = val,
                        () => skillComp.BuildingExp, val => skillComp.BuildingExp = val),
        ["MeleeWeapon"] = (() => skillComp.MeleeWeaponLevel, val => skillComp.MeleeWeaponLevel = val,
                        () => skillComp.MeleeWeaponExp, val => skillComp.MeleeWeaponExp = val)
    };

        // Проверяем, существует ли данный тип навыка
        if (!skills.TryGetValue(skillType, out var skill))
            return false;

        var (getLevel, setLevel, getExp, setExp) = skill;

        int level = getLevel();
        int exp = getExp();

        // Проверка ограничения уровня
        if (level >= maxLevel || level >= 3)
            return false;

        // Расчёт необходимого опыта
        int requiredExp = 300 + level * 300;
        exp += experienceAmount;
        setExp(exp);

        // Проверка на повышение уровня
        if (exp >= requiredExp)
        {
            setLevel(level + 1);
            setExp(exp-requiredExp);
            skillComp.Dirty();
            return true;
        }

        skillComp.Dirty();
        return false;
    }

}