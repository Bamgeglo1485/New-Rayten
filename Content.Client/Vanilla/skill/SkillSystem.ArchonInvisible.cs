using Content.Shared.Vanilla.Skill;
using Robust.Client.Player;
using Robust.Client.GameObjects;

namespace Content.Client.Vanilla.Skill;

public sealed partial class SkillSystem : SharedSkillSystem
{

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

    private void UpdateAllInvisibleArchons()
    {
        var query = EntityQueryEnumerator<SkillInvisibleComponent>();
        while (query.MoveNext(out var ent, out var comp))
            UpdateVisibility(ent, comp);
    }

    public void UpdateVisibility(EntityUid uid, SkillInvisibleComponent? comp = null)
    {
        if (!Resolve(uid, ref comp))
            return;

        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        var locEnt = _player.LocalSession?.AttachedEntity;
        if (locEnt == null)
            return;

        if (comp.Visible || locEnt == uid)
        {
            sprite.Visible = true;
            return;
        }
        sprite.Visible = HasRequiredSkill(locEnt.Value, SkillType.Research, WithBeep: false);
    }
}
