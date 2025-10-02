using Content.Shared.Vanilla.Entities.SecuritronWhistle;
using Content.Shared.Vanilla.Dominator;
namespace Content.Client.Vanilla.Entities.SecuritronWhistle;

public sealed class SecuritronWhistleSystem : SharedSecuritronWhistleSystem
{
    protected override void FollowMe(EntityUid target, EntityUid master, SecurityMarkerComponent comp, SecuritronMasterComponent mastercomp)
    {
    }
}
