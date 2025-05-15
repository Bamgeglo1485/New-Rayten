using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Content.Server.Administration.Logs;
using Content.Shared.Vanilla.Background;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Vanilla.Skill;
using Content.Shared.Roles;
using Content.Server.SkillTrainer;
using Content.Server.Mind;
using Content.Shared.Mind;
using Content.Server.Roles;
using Content.Server.Ghost.Roles;
using Content.Server.Administration.Systems;
using Content.Shared.Administration;
using Content.Server.Vanilla.Skill;
using Content.Shared.Vanilla.Jammer;

namespace Content.Server.Vanilla.Background;

public sealed class BackGroundSystem : EntitySystem
{
    [Dependency] private readonly ServerSkillTrainerSystem _skillTrainer = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly AdminFrozenSystem _freeze = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AwaitBackgroundComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<AwaitBackgroundComponent, ComponentShutdown>(OnShutdown);
        SubscribeNetworkEvent<TakeGhostBackgroundEvent>(OnTakeGhostBackgroundEvent);
    }
    public void ApplyBackground(EntityUid uid, RoleBackground? rolebackground)
    {   
        if (rolebackground == null)
            return;
        //итоговые навыки
        Dictionary<skillType, SkillLevel> generalbasicskills = new()
        {
            { skillType.RangeWeapon, SkillLevel.None },
            { skillType.MeleeWeapon, SkillLevel.None },
            { skillType.Medicine, SkillLevel.None },
            { skillType.Chemistry, SkillLevel.None },
            { skillType.Engineering, SkillLevel.None },
            { skillType.Building, SkillLevel.None },
            { skillType.Research, SkillLevel.None },
            { skillType.Crime, SkillLevel.None }
        };
        HashSet<skillType> generaleasyskills = new();
        //итоговые особенности
        List<BackgroundSpecial> generalSpecials = new();

        //обнуляем навык и предысторию
        RemComp<SkillComponent>(uid);      
        var skillComp = EnsureComp<SkillComponent>(uid);
        var backgroundcomp = EnsureComp<BackgroundComponent>(uid);

        //Складываем навыки и особенности с предысторий
        if (_prototype.TryIndex(rolebackground.SelectedBabyBackground, out var bgProtoBaby))
        {
            layDownBacic(bgProtoBaby.Skills);
            layDownEasy(bgProtoBaby.EasySkills);
            generalSpecials.AddRange(bgProtoBaby.Specials);
            backgroundcomp.BabyBackground = bgProtoBaby;
        }
        if (_prototype.TryIndex(rolebackground.SelectedAdultBackground, out var bgProtoAdult))
        {
            layDownBacic(bgProtoAdult.Skills);
            layDownEasy(bgProtoAdult.EasySkills);
            generalSpecials.AddRange(bgProtoAdult.Specials);
            backgroundcomp.AdultBackground = bgProtoAdult;
        }
        if (_prototype.TryIndex(rolebackground.SelectedGeneralBackground, out var bgProtoGeneral))
        {
            layDownBacic(bgProtoGeneral.Skills);
            layDownEasy(bgProtoGeneral.EasySkills);
            generalSpecials.AddRange(bgProtoGeneral.Specials);
            backgroundcomp.GeneralBackground = bgProtoGeneral;
        }
        layDownBacic(rolebackground.AddedBasicSkills);
        layDownEasy(rolebackground.AddedEasySkills);

        //Передаём навыки и особенности в сущность
        ApplySkills(uid, skillComp, generalbasicskills);
        ApplyEasySkills(uid, skillComp, generaleasyskills);
        ApplySpecials(uid, generalSpecials);

        Dirty(uid, backgroundcomp);

        void layDownBacic(Dictionary<skillType, SkillLevel>? backgroundSkills)
        {
            if (backgroundSkills == null)
                return;

            foreach (var (skill, level) in backgroundSkills)
            {
                if (!generalbasicskills.TryGetValue(skill, out var currentLevel))
                    continue;

                int total = (int)currentLevel + (int)level;
                SkillLevel newLevel = total > (int)SkillLevel.Expert
                    ? SkillLevel.Expert
                    : (SkillLevel)total;

                generalbasicskills[skill] = newLevel;
            }
        }
        void layDownEasy(HashSet<skillType>? backgroundSkills)
        {
            if (backgroundSkills == null)
                return;

            foreach (var skill in backgroundSkills)
            {
                generaleasyskills.Add(skill);
            }
        }
    }
    private void OnMapInit(EntityUid uid, AwaitBackgroundComponent component, MapInitEvent args)
    {
        _freeze.FreezeAndMute(uid);
    }

    private void OnShutdown(EntityUid uid, AwaitBackgroundComponent component, ComponentShutdown args)
    {
        RemComp<AdminFrozenComponent>(uid);
    }

    private void OnTakeGhostBackgroundEvent(TakeGhostBackgroundEvent msg, EntitySessionEventArgs args)
    {
        if (!args.SenderSession.AttachedEntity.HasValue)
            return;

        var uid = args.SenderSession.AttachedEntity.Value;

        if (!HasComp<AwaitBackgroundComponent>(uid))
            return;

        RemComp<AwaitBackgroundComponent>(uid);

        if (_prototype.TryIndex(msg.Background, out var bgProto))
        {
            RemComp<SkillComponent>(uid);      
            var skillComp = EnsureComp<SkillComponent>(uid);

            ApplySkills(uid, skillComp, bgProto.Skills);
            ApplyEasySkills(uid, skillComp, bgProto.EasySkills);
            ApplySkillPoints(uid, skillComp, bgProto.SkillPoints);
            ApplySpecials(uid, bgProto.Specials);

            var backgroundcomp = EnsureComp<BackgroundComponent>(uid);
            backgroundcomp.GeneralBackground = msg.Background;
            Dirty(uid, backgroundcomp);
        }
        else
        {
            Log.Error($"Не удалось найти предысторию с ID {msg.Background}");            
        }
    }
    private void ApplySkillPoints(EntityUid uid, SkillComponent skillComp, int SkillPoints)
    {
        skillComp.SkillPoints += SkillPoints;
    }
    
    private void ApplySkills(EntityUid uid, SkillComponent skillComp, Dictionary<skillType, SkillLevel> Skills)
    {
        foreach (var (skillType, level) in Skills)
        {
            _skillTrainer.SetSkillLevel(skillComp, skillType, level);
        }
    }
    private void ApplyEasySkills(EntityUid uid, SkillComponent skillComp, HashSet<skillType> EasySkills)
    {
        foreach (var skillType in EasySkills)
        {
            _skillTrainer.SetEasySkill(skillComp, skillType);
        }
    }

    private void ApplySpecials(EntityUid uid, List<BackgroundSpecial> Specials)
    {
        foreach (var Special in Specials)
        {
            Special.apply(uid);
        }
    }
}