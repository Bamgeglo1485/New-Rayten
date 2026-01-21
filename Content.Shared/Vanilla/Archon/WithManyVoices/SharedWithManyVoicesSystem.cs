using Content.Shared.Animals.Components;
using Content.Shared.Vanilla.Archon.BlindPredator;
using Content.Shared.Animals.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Bed.Sleep;
using Content.Shared.Chat;
using Content.Shared.Popups;
using Content.Shared.Speech;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics.Components;
using Robust.Shared.Timing;
using Robust.Shared.Random;
namespace Content.Shared.Vanilla.Archon.WithManyVoices;

public abstract class SharedWithManyVoicesSystem : EntitySystem
{
    [Dependency] protected readonly IGameTiming Timing = default!;
    [Dependency] private readonly SharedBlindPredatorSystem _predator = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedChatSystem _chat = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WithManyVoicesComponent, WithManyVoicesExoEvent>(OnExoEvent);
        SubscribeLocalEvent<WithManyVoicesComponent, ListenEvent>(OnListen);
    }

    private void OnListen(EntityUid uid, WithManyVoicesComponent comp, ref ListenEvent args)
    {
        if (TryComp<PredatorVisibleMarkComponent>(args.Source, out var mark))
            _predator.SetVisibility(args.Source, uid, true, mark);

        if (comp.SeeResetAt == null)
            comp.SeeResetAt = Timing.CurTime + comp.SeeTime;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var query = EntityQueryEnumerator<WithManyVoicesComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.SeeResetAt == null)
                continue;

            if (Timing.CurTime < comp.SeeResetAt)
                continue;

            comp.SeeResetAt = null;
            Dirty(uid, comp);

            var victimQuery = EntityQueryEnumerator<PredatorVisibleMarkComponent>();
            while (victimQuery.MoveNext(out var ent, out var mark))
                _predator.SetVisibility(ent, uid, false, mark);

            Replan(uid);
        }
    }

    private void OnExoEvent(EntityUid uid, WithManyVoicesComponent comp, WithManyVoicesExoEvent args)
    {
        if (args.Handled)
            return;

        if (HasComp<SleepingComponent>(uid))
            return;

        if (!TryComp<ParrotMemoryComponent>(uid, out var parrotComp))
            return;

        if (parrotComp.SpeechMemories.Count <= 0)
        {
            _popup.PopupClient("Вы еще не скопировали ничей голос", uid, uid, PopupType.Medium);
            return;
        }

        comp.SeeResetAt = Timing.CurTime + comp.SeeTime;

        var uidTrans = Transform(uid).Coordinates;
        var victimQuery = EntityQueryEnumerator<InputMoverComponent, PredatorVisibleMarkComponent, PhysicsComponent, TransformComponent>();
        while (victimQuery.MoveNext(out var targetUid, out var input, out var mark, out var physics, out var xform))
        {
            var visibleDistance = comp.VisibleDistanceRun;
            if (!input.Sprinting)
                continue;

            if (physics.LinearVelocity.Length() < 0.1f)
                visibleDistance = comp.VisibleDistanceStand;

            if (!uidTrans.TryDistance(EntityManager, xform.Coordinates, out var distance))
                continue;

            _predator.SetVisibility(targetUid, uid, distance <= visibleDistance, mark);
        }

        var memory = _random.Pick(parrotComp.SpeechMemories);
        _chat.TrySendInGameICMessage(uid, memory.Message, InGameICChatType.Speak, false, nameOverride: memory.Name, ignoreActionBlocker: true);
        parrotComp.SpeechMemories.Remove(memory);
        Dirty(uid, parrotComp);
        _audio.PlayPredicted(comp.ExoSound, uid, uid);
        Replan(uid);
        args.Handled = true;
    }

    protected abstract void Replan(EntityUid uid);
}
