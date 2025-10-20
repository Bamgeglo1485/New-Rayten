using Content.Server.Chat.Systems;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Server.NPC.HTN.PrimitiveTasks.Operators;
using Content.Server.Radio.EntitySystems;
using Content.Shared.Dataset;
using Content.Shared.Chat;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Vanilla.NPC.HTN.PrimitiveTasks.Operators;

public sealed partial class SpeakDatasetOperator : HTNOperator
{
    private ChatSystem _chat = default!;
    private RadioSystem _radio = default!;

    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    [DataField(required: true)]
    public ProtoId<LocalizedDatasetPrototype> Dataset = string.Empty;

    [DataField]
    public bool Hidden;

    [DataField]
    public bool Radio = false;

    [DataField]
    public string RadioChanel = "Security";

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);

        _chat = sysManager.GetEntitySystem<ChatSystem>();
        _radio = sysManager.GetEntitySystem<RadioSystem>();
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        var speaker = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        var dataset = _proto.Index(Dataset);
        var pick = _random.Pick(dataset.Values);

        if (Radio)
        {
            _radio.SendRadioMessage(speaker, Loc.GetString(pick), RadioChanel, speaker);
        }
        else
        {
            _chat.TrySendInGameICMessage(
                speaker,
                Loc.GetString(pick),
                InGameICChatType.Speak,
                hideChat: Hidden,
                hideLog: Hidden);
        }

        return HTNOperatorStatus.Finished;
    }
}
