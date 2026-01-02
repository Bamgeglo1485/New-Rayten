using Content.Shared.Vanilla.Skill;
using Robust.Client.Player;
using Robust.Client.GameObjects;

namespace Content.Client.Vanilla.Skill;

public sealed class SkillInvisibleSystem : SharedSkillInvisibleSystem
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly RequiresSkillSystem _reqskill = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SkillInvisibleComponent, AfterAutoHandleStateEvent>(OnHandleState);
        SubscribeLocalEvent<SkillInvisibleComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<SkillInvisibleComponent, ComponentStartup>(OnStartup);
    }

    private void OnHandleState(EntityUid uid, SkillInvisibleComponent comp, ref AfterAutoHandleStateEvent args)
    {
        UpdateVisibility(uid, comp);
    }

    private void OnStartup(EntityUid uid, SkillInvisibleComponent comp, ref ComponentStartup args)
    {
        UpdateVisibility(uid, comp);
    }

    private void OnShutdown(EntityUid uid, SkillInvisibleComponent comp, ref ComponentShutdown args)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        sprite.Visible = true;
    }

    public void UpdateVisibility(EntityUid uid, SkillInvisibleComponent? comp = null)
    {
        if (!Resolve(uid, ref comp))
            return;

        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        var locEnt = _playerManager.LocalSession?.AttachedEntity;
        if (locEnt == null)
            return;

        if (comp.Visible || locEnt == uid)
        {
            sprite.Visible = true;
            return;
        }

        if (!TryComp<SkillComponent>(locEnt.Value, out var skill))
        {
            sprite.Visible = false;
            return;
        }

        sprite.Visible = skill.Research;
    }
}
