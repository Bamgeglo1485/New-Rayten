using Content.Server.Roles;
using Content.Server.Ghost.Roles;
using Content.Server.Administration.Systems;
using Content.Shared.Administration;
using Content.Server.Vanilla.Skill;
using Content.Server.GameTicking.Events;
using Content.Server.Preferences.Managers;
using Content.Shared.Vanilla.Background;
using Content.Shared.Vanilla.TDM;
using Content.Shared.Vanilla.Skill;
using Content.Shared.Roles;
using Content.Shared.Preferences;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Server.Vanilla.Background;

public sealed class BackGroundSystem : EntitySystem
{
    [Dependency] private readonly SkillTrainerSystem _skillTrainer = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
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
        if (!HasComp<TDMMarkerComponent>(uid))
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
