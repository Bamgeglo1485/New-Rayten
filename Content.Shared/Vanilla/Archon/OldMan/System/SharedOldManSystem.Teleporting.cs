using Content.Shared.Administration;
namespace Content.Shared.Vanilla.Archon.OldMan;

public abstract partial class SharedOldManSystem : EntitySystem
{
    protected void TeleportAnimation(EntityUid uid, bool isOut = false)
    {
        var anim = EnsureComp<PDAnimationComponent>(uid);
        anim.TeleportationEndAt = timing.CurTime + TimeSpan.FromSeconds(anim.TeleportDuration);
        anim.IsOut = isOut;
        EnsureComp<AdminFrozenComponent>(uid);
        TryStartAnimation(uid, anim);
    }

    protected abstract void TryStartAnimation(EntityUid uid, PDAnimationComponent comp);
    protected abstract void TryStopAnimation(EntityUid uid);
}
