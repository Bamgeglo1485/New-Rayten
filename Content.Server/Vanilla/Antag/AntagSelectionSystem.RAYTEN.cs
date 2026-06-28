using System.Linq;
using Content.Server.Antag.Components;
using Content.Server.Antag.Selectors;
using Content.Shared.Antag;
using Content.Shared.Chat;
using Content.Shared.GameTicking.Components;
using Content.Shared.Ghost;
using Content.Shared.Mind;
using Content.Shared.Roles;
using JetBrains.Annotations;
using Robust.Shared.Audio;
using Robust.Shared.Enums;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Content.Shared.Vanilla.AntagAccept;
using Content.Server.Vanilla.AntagAccept;
using Content.Server.EUI;
using Robust.Shared.Timing;
using System.Collections.Generic;

namespace Content.Server.Antag;

public sealed partial class AntagSelectionSystem
{
    [Dependency] private readonly EuiManager _eui = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_pendingConfirmations != null && _pendingConfirmations.Count > 0)
            CheckPendingConfirmations();
    }

    private void CheckPendingConfirmations()
    {
        var now = _gameTiming.RealTime;
        var expired = _pendingConfirmations
            .Where(kvp => kvp.Value.Timeout < now)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var session in expired)
        {
            _pendingConfirmations.Remove(session);
            OnAntagAcceptDenied(session);
        }
    }

    private void RequestAntagAccept(Entity<AntagSelectionComponent> gameRule,
        AntagSpecifierPrototype definition,
        ICommonSession player,
        AntagCount[] antags,
        AntagCount currentAntag,
        int index)
    {
        var roleName = definition.ID.ToString();

        _pendingConfirmations[player] = (
            gameRule,
            antags,
            currentAntag,
            _gameTiming.RealTime.Add(TimeSpan.FromSeconds(60)),
            index
        );

        var eui = new AntagAcceptEui(definition, roleName, player);
        _eui.OpenEui(eui, player);
    }

    public void OnAntagAcceptMessage(AntagAcceptMessage message, ICommonSession session)
    {
        if (!_pendingConfirmations.TryGetValue(session, out var pending))
            return;

        if (message.Accepted)
        {
            OnAntagAcceptAccepted(session, pending);
        }
        else
        {
            OnAntagAcceptDenied(session);
        }
    }

    private void OnAntagAcceptAccepted(ICommonSession session, (Entity<AntagSelectionComponent> GameRule, AntagCount[] Antags, AntagCount CurrentAntag, TimeSpan Timeout, int Index) pending)
    {
        _pendingConfirmations.Remove(session);

        if (AssignPlayerAsAntag(session, pending.GameRule, pending.Antags, pending.CurrentAntag, pending.Index))
        {
            return;
        }
        else
        {
            ContinueAntagAssignment(pending.GameRule, pending.Antags, pending.CurrentAntag, session);
        }
    }

    private void OnAntagAcceptDenied(ICommonSession session)
    {
        if (!_pendingConfirmations.TryGetValue(session, out var pending))
            return;

        _pendingConfirmations.Remove(session);
        ContinueAntagAssignment(pending.GameRule, pending.Antags, pending.CurrentAntag, session);
    }

    private void ContinueAntagAssignment(Entity<AntagSelectionComponent> gameRule,
        AntagCount[] antags,
        AntagCount currentAntag,
        ICommonSession? skippedPlayer = null)
    {
        var players = GetActivePlayers().ToArray();
        var weightedPool = GetWeightedPlayerPool(players);

        if (skippedPlayer != null && weightedPool.ContainsKey(skippedPlayer))
            weightedPool.Remove(skippedPlayer);

        while (weightedPool.Count > 0)
        {
            var session = weightedPool.Keys.ElementAt(RobustRandom.Next(weightedPool.Count));
            weightedPool.Remove(session);

            var newAntags = antags.ToArray();
            if (AssignAntag(gameRule, session, ref newAntags))
            {
                for (int i = 0; i < antags.Length; i++)
                {
                    antags[i] = newAntags[i];
                }
                return;
            }
        }

        SpawnGhostRoles(gameRule, antags);
    }
}
