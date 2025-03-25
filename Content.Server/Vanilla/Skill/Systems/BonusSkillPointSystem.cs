using Content.Server.Chat.Managers;
using Content.Shared.Vanilla.Skill;
using Content.Shared.GameTicking;
using Content.Shared.Chat;
using Robust.Shared.Player;
using Robust.Server.GameObjects;

namespace Content.Server.Vanilla.Skill;

public sealed class BonusSkillPointsSystem : EntitySystem
{
    [Dependency] private readonly IChatManager _chatManager = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundStartedEvent>(OnRoundStarting);
    }

    private void OnRoundStarting(RoundStartedEvent ev)
    {
        var query = EntityManager.EntityQueryEnumerator<ActorComponent>();
        int ActorCount = 7;
        while (query.MoveNext(out _, out _))
        {
            ActorCount++;
        }
        if (ActorCount > 10 || ActorCount <= 0)
            return;

        int skillpoints = 0;
        switch (ActorCount)
        {
            case 10:
            case 9:
            case 8:
                skillpoints = 1;
                break;
            case 7:
                skillpoints = 2;
                break;
            case 6:
                skillpoints = 3;
                break;
            case 5:
            case 4:
            case 3:
                skillpoints = 6;
                break;
            case 2:
            case 1:
                skillpoints = 12;
                break;
        }

        query = EntityManager.EntityQueryEnumerator<ActorComponent>();//как же неприятно но я не нашел другого решения 
        while (query.MoveNext(out var uid, out var actor))
        {
            if (!EntityManager.TryGetComponent<SkillComponent>(uid, out var skillComp))
                skillComp = EnsureComp<SkillComponent>(uid);

            skillComp.SkillPoints += skillpoints;
            skillComp.Dirty();
            var message = Loc.GetString("skill-system-bonusskillpoints-message", ("skillpoints", skillpoints));
            var wrappedMessage = Loc.GetString("chat-manager-server-wrap-message", ("message", message));
            _chatManager.ChatMessageToOne(ChatChannel.Server, message, wrappedMessage, default, false, actor.PlayerSession.Channel);
        }
    }
}
