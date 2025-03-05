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
        int ActorCount=0;
        while (query.MoveNext(out _, out _))
        {
            ActorCount++;
        }
        int skillpoints = ActorCount > 0 ? Math.Max((int)(10 / Math.Pow(ActorCount, 0.5)), 1) : 0;

        if (ActorCount > 10 || ActorCount == 0)
            return;

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
