using Content.Server.Vanilla.Objectives.Components;
using Content.Shared.Objectives.Components;
using Content.Shared.Vanilla.Archon.PlagueDoctor;

namespace Content.Server.Vanilla.Objectives.Systems;

public sealed class PestilenceConditionSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PestilenceConditionComponent, ObjectiveGetProgressEvent>(OnGetProgress);
    }

    private void OnGetProgress(EntityUid uid, PestilenceConditionComponent comp, ref ObjectiveGetProgressEvent args)
    {
        if (!TryComp<PlagueDoctorComponent>(GetEntity(args.Mind.OriginalOwnedEntity), out var doctor))
            return;

        args.Progress = doctor.State == PlagueDoctorState.Safe ? 1f : 0f;
    }
}