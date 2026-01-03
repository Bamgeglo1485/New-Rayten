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
    [Dependency] private readonly SharedSkillSystem _skill = default!;
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

            skillComp.BasicSkills = bgProto.Skills;
            skillComp.EasySkills = bgProto.EasySkills;
            skillComp.SkillPoints = bgProto.SkillPoints;
            ApplySpecials(uid, bgProto.Specials);

            var backgroundcomp = EnsureComp<BackgroundComponent>(uid);
            backgroundcomp.GeneralBackground = msg.Background;
            Dirty(uid, backgroundcomp);
            Dirty(uid, skillComp);
        }
        else
        {
            Log.Error($"Не удалось найти предысторию с ID {msg.Background}");
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
