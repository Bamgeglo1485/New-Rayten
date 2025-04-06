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
namespace Content.Server.Vanilla.Background;

public sealed class BackGroundSystem : EntitySystem
{
    [Dependency] private readonly ServerSkillTrainerSystem _skillTrainer = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly RoleSystem  _role = default!;
    [Dependency] private readonly AdminFrozenSystem _freeze = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AwaitBackgroundComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<AwaitBackgroundComponent, ComponentShutdown>(OnShutdown);
        SubscribeNetworkEvent<TakeGhostBackgroundEvent>(OnTakeGhostBackgroundEvent);
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

        if (_prototype.TryIndex(msg.Background, out var bgProto))
        {
            RemComp<SkillComponent>(uid);      
            var skillComp = EnsureComp<SkillComponent>(uid);

            ApplySkillsFromGhostBackground(uid, skillComp, bgProto.Skills);
            ApplyEasySkillsFromGhostBackground(uid, skillComp, bgProto.EasySkills);
            ApplySkillPointsFromGhostBackground(uid, skillComp, bgProto.SkillPoints);
            ApplySpecialsFromGhostBackground(uid, bgProto.Specials);

            RemComp<AwaitBackgroundComponent>(uid);

            var backgroundcomp = EnsureComp<BackgroundComponent>(uid);
            backgroundcomp.Background = msg.Background;
            Dirty(uid, backgroundcomp);
        }
        else
        {
            Log.Error($"Не удалось найти предысторию с ID {msg.Background}");       
            RemComp<AwaitBackgroundComponent>(uid);         
        }
    }
    private void ApplySkillPointsFromGhostBackground(EntityUid uid, SkillComponent skillComp, int SkillPoints)
    {
        skillComp.SkillPoints += SkillPoints;
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

private void ApplySpecialsFromGhostBackground(EntityUid uid, HashSet<ProtoId<BackgroundSpecialPrototype>> Specials)
{
    if (!_mind.TryGetMind(uid, out var mindId, out var mindcomp))
        return;

    foreach (var specialId in Specials)
    {
        if (!_prototype.TryIndex<BackgroundSpecialPrototype>(specialId, out var special))
        {
            Log.Error($"прототипа {specialId} не существует");       
            continue;
        }

        if (special?.MindRoles is { } mindRoles)
        {
            _role.MindTryRemoveRole<MindRoleComponent>(mindId);
            _role.MindTryRemoveRole<GhostRoleMarkerRoleComponent>(mindId);
            _role.MindTryRemoveRole<NukeopsRoleComponent>(mindId);
            _role.MindAddRoles(mindId, mindRoles, mindcomp);
        }

        if (special?.Items is { } SomeItems)
        {
            foreach (var someitem in SomeItems)
            {
                var item = Spawn(someitem, Transform(uid).Coordinates);
                _hands.PickupOrDrop(uid, item);
            }
        }
    }
}



}