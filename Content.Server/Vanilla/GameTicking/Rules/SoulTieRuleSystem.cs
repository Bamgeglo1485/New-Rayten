using Content.Server.GameTicking.Rules.Components;
using Content.Server.Station.Systems;
using Content.Shared.Chat;
using Content.Shared.GameTicking.Components;
using Content.Shared.Mobs;
using Content.Shared.Players;
using Robust.Shared.Network;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using Content.Shared.Mind.Components;
using Robust.Server.Audio;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Damage;
using Content.Shared.Objectives.Systems;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Atmos.Components;
using Content.Shared.Vanilla.SoulTie;
using Content.Shared.Mind;
using Content.Shared.Mobs.Systems;
using Content.Shared.Mobs.Components;
using Content.Shared.Body;
using Content.Shared.Station.Components;
using Content.Server.RoundEnd;
using System.Numerics;

namespace Content.Server.GameTicking.Rules;

/// <summary>
/// Связывает двух людей, из-за чего те разделяет урон и должны находится рядом
/// </summary>
public sealed partial class SoulTieRuleSystem : GameRuleSystem<SoulTieRuleComponent>
{
    [Dependency] private SharedTransformSystem _transformSystem = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private RoundEndSystem _roundEnd = default!;
    [Dependency] private AtmosphereSystem _atmos = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private AudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SoulTiedComponent, DamageChangedEvent>(SoulTiedDamaged);
    }

    private TimeSpan _nextUpdate = TimeSpan.Zero;
    private const float UpdateInterval = 30f;
    private int _activeRulesCount = 0;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_activeRulesCount <= 0)
            return;

        var currentTime = _timing.CurTime;
        if (currentTime < _nextUpdate)
            return;

        _nextUpdate = currentTime + TimeSpan.FromSeconds(UpdateInterval);

        UpdateAllSoulTies();
    }

    private void UpdateAllSoulTies()
    {
        var query = EntityQueryEnumerator<SoulTieRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var soulTieRule, out var gameRule))
        {
            if (soulTieRule.First == null || soulTieRule.Second == null)
                continue;

            if (soulTieRule.FirstTransform == null || soulTieRule.SecondTransform == null)
                continue;

            var firstPos = soulTieRule.FirstTransform.Coordinates;
            var secondPos = soulTieRule.SecondTransform.Coordinates;
            var distance = Vector2.Distance(secondPos.Position, firstPos.Position);

            if (distance > soulTieRule.TeleportDistance)
            {
                _transformSystem.SetCoordinates(soulTieRule.First.Value, soulTieRule.SecondTransform.Coordinates);
                _audio.PlayPvs(soulTieRule.TeleportSound, soulTieRule.Second.Value);
            }
        }
    }

    private void SoulTiedDamaged(EntityUid uid, SoulTiedComponent comp, DamageChangedEvent args)
    {
        if (args.DamageDelta == null || comp.Another == null || comp.AnotherSoulTied == null)
            return;

        if (comp.Damaged)
        {
            comp.Damaged = false;
            return;
        }

        comp.AnotherSoulTied.Damaged = true;

        _damageable.ChangeDamage(
            comp.Another.Value,
            args.DamageDelta,
            origin: args.Origin,
            ignoreResistances: true,
            interruptsDoAfters: true);
    }

    protected override void Started(EntityUid uid, SoulTieRuleComponent comp, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, comp, gameRule, args);

        if (!SetTargets(comp))
        {
            ForceEndSelf(uid, gameRule);
        return;
        }

        if (comp.First == null || comp.Second == null)
        {
            ForceEndSelf(uid, gameRule);
        return;
        }

        if (comp.FirstTransform == null || comp.SecondTransform == null)
        {
            ForceEndSelf(uid, gameRule);
            return;
        }

        _transformSystem.SetCoordinates(comp.First.Value, comp.SecondTransform.Coordinates);
        _audio.PlayPvs(comp.TeleportSound, comp.Second.Value);

        _activeRulesCount++;
    }

    protected override void Ended(EntityUid uid, SoulTieRuleComponent comp, GameRuleComponent gameRule, GameRuleEndedEvent args)
    {
        base.Ended(uid, comp, gameRule, args);

        if (comp.First != null)
        {
            if (HasComp<SoulTiedComponent>(comp.First.Value))
            {
                RemComp<SoulTiedComponent>(comp.First.Value);
            }
        }
        if (comp.Second != null)
        {
            if (HasComp<SoulTiedComponent>(comp.Second.Value))
            {
                RemComp<SoulTiedComponent>(comp.Second.Value);
            }
        }

        comp.First = null;
        comp.Second = null;

        _activeRulesCount--;
    }

    public bool SetTargets(SoulTieRuleComponent comp)
    {
        var station = _roundEnd.GetStation();

        if (station == null)
            return false;

        var valid_targets = new List<EntityUid>();

        var query = EntityQueryEnumerator<BodyComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out _, out var mobState))
        {
            if (!_mobState.IsAlive(uid, mobState))
                continue;

            if (!_mind.TryGetMind(uid, out _, out _))
                continue;

            var transform = Transform(uid);
            if (transform.MapUid != station)
                continue;
            
            var mixture = _atmos.GetTileMixture(uid);
            if (mixture is null || mixture.TotalMoles < 40f)
                continue;

            valid_targets.Add(uid);
        }

        if (valid_targets.Count < 2)
            return false;

        var first_index = _random.Next(valid_targets.Count);
        comp.First = valid_targets[first_index];
        comp.FirstTransform = Transform(comp.First.Value);
        valid_targets.RemoveAt(first_index);

        var second_index = _random.Next(valid_targets.Count);
        comp.Second = valid_targets[second_index];
        comp.SecondTransform = Transform(comp.Second.Value);

        EnsureComp<SoulTiedComponent>(comp.First.Value, out var soulComp);
        soulComp.Another = comp.Second.Value;

        EnsureComp<SoulTiedComponent>(comp.Second.Value, out var SecondSoulComp);
        SecondSoulComp.Another = comp.First.Value;

        soulComp.AnotherSoulTied = SecondSoulComp;
        SecondSoulComp.AnotherSoulTied = soulComp;

        return true;
    }
}
