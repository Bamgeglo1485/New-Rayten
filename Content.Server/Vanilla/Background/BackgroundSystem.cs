using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Content.Server.Administration.Logs;
using Content.Shared.Vanilla.Background;
using Content.Shared.Vanilla.Skill;
using Content.Server.SkillTrainer;
using Content.Server.Mind;
using Content.Shared.Mind;
using Content.Shared.Database;
using Content.Shared.Roles;

namespace Content.Server.Vanilla.Background;

public sealed class BackGroundSystem : EntitySystem
{
    [Dependency] private readonly ServerSkillTrainerSystem _skillTrainer = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<TakeGhostBackgroundEvent>(OnTakeGhostBackgroundEvent);
    }
    private void OnTakeGhostBackgroundEvent(TakeGhostBackgroundEvent msg, EntitySessionEventArgs args)
    {
        if (!args.SenderSession.AttachedEntity.HasValue)
            return;
        var uid = args.SenderSession.AttachedEntity.Value;

        if (!HasComp<AwaitBackgroundComponent>(uid))
            return;

        if (_prototype.TryIndex(msg.Background, out var bgProto))
        {
            RemComp<SkillComponent>(uid);      
            var skillComp = EnsureComp<SkillComponent>(uid);
            ApplySkillsFromGhostBackground(uid, skillComp, bgProto.Skills);
            ApplyEasySkillsFromGhostBackground(uid, skillComp, bgProto.EasySkills);
            ApplySpecialsFromGhostBackground(uid, bgProto.Specials);
            RemComp<AwaitBackgroundComponent>(uid);
        }
        else
        {
            Log.Error($"Не удалось найти предысторию с ID {msg.Background}");       
            RemComp<AwaitBackgroundComponent>(uid);         
        }
    }
    private void ApplySkillsFromGhostBackground(EntityUid uid, SkillComponent skillComp, Dictionary<skillType, SkillLevel> Skills)
    {
        foreach (var (skillType, level) in Skills)
        {
            _skillTrainer.SetSkillLevel(skillComp, skillType, level);
        }
    }
    private void ApplyEasySkillsFromGhostBackground(EntityUid uid, SkillComponent skillComp, HashSet<skillType> EasySkills)
    {
        foreach (var skillType in EasySkills)
        {
            _skillTrainer.SetEasySkill(skillComp, skillType);
        }
    }

    private void ApplySpecialsFromGhostBackground(EntityUid uid, HashSet<BackgroundSpecial> Specials)
    {
        if (!_mind.TryGetMind(uid, out var mindId, out _))
            return;

        foreach (var Special in Specials)
        {
            switch(Special)
            {
                case BackgroundSpecial.MakeAntag:
                    SetRoleType(mindId, "SoloAntagonist");
                break;
                case BackgroundSpecial.MakeNonAntag:
                    SetRoleType(mindId, "Neutral");
                break;
                case BackgroundSpecial.MakeFreeAgent:
                    SetRoleType(mindId, "FreeAgent");
                break;
                case BackgroundSpecial.RandomMagic:
                break;
            }

        }
    }


    //Нахуя разрабы сделали его приватным блять?
    private void SetRoleType(EntityUid mind, ProtoId<RoleTypePrototype> roleTypeId)
    {
        if (!TryComp<MindComponent>(mind, out var comp))
        {
            Log.Error($"Failed to update Role Type of mind entity {ToPrettyString(mind)} to {roleTypeId}. MindComponent not found.");
            return;
        }

        if (!_prototype.HasIndex(roleTypeId))
        {
            Log.Error($"Failed to change Role Type of {_mind.MindOwnerLoggingString(comp)} to {roleTypeId}. Invalid role");
            return;
        }

        comp.RoleType = roleTypeId;
        Dirty(mind, comp);

        // Update player character window
        if (_mind.TryGetSession(mind, out var session))
            RaiseNetworkEvent(new MindRoleTypeChangedEvent(), session.Channel);
        else
        {
            var error = $"The Character Window of {_mind.MindOwnerLoggingString(comp)} potentially did not update immediately : session error";
            _adminLogger.Add(LogType.Mind, LogImpact.Medium, $"{error}");
        }

        if (comp.OwnedEntity is null)
        {
            Log.Error($"{ToPrettyString(mind)} does not have an OwnedEntity!");
            _adminLogger.Add(LogType.Mind,
                LogImpact.Medium,
                $"Role Type of {ToPrettyString(mind)} changed to {roleTypeId}");
            return;
        }

        _adminLogger.Add(LogType.Mind,
            LogImpact.High,
            $"Role Type of {ToPrettyString(comp.OwnedEntity)} changed to {roleTypeId}");
    }
}