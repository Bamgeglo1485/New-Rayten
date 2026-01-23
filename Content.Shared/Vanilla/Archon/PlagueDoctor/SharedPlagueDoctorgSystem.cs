using Content.Shared.Administration;
using Content.Shared.Alert;
using Content.Shared.Actions;
using Content.Shared.Audio;
using Content.Shared.Mobs.Systems;
using Content.Shared.Mobs.Components;
using Content.Shared.DoAfter;
using Content.Shared.Popups;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Vanilla.Archon.Research;
using Content.Shared.Vanilla.Archon.BlindPredator;
using Content.Shared.Zombies;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Humanoid;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;
using Robust.Shared.Player;

namespace Content.Shared.Vanilla.Archon.PlagueDoctor;

public abstract class SharedPlagueDoctorgSystem : EntitySystem
{
    [Dependency] protected readonly SharedPopupSystem Popup = default!;
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly MobStateSystem _mob = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedAmbientSoundSystem _ambient = default!;
    [Dependency] private readonly SharedBlindPredatorSystem _predator = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedArchonResearchSystem _archon = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlagueDoctorComponent, ResearchAttemptEvent>(OnResearchAttempt);
        SubscribeLocalEvent<PlagueDoctorComponent, ResearchLinkDisconnectionEvent>(OnDisconnect);
        SubscribeLocalEvent<PlagueDoctorComponent, Surgery049Event>(OnSurgeryAction);
        SubscribeLocalEvent<PlagueDoctorComponent, Surgery049DoAfterEvent>(OnSurgeryDoAfter);
        SubscribeLocalEvent<PlagueDoctorComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<PlagueDoctorComponent, DamageChangedEvent>(OnDamageChange);
        SubscribeLocalEvent<PlagueDoctorComponent, BeforeDamageChangedEvent>(OnBeforeDamageChanged);
        SubscribeLocalEvent<PlagueDoctorComponent, MeleeHitEvent>(OnMeleeHit);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<PlagueDoctorComponent>();
        while (query.MoveNext(out var uid, out var docComp))
        {
            ProcessAnimation(uid, docComp);
            if (now < docComp.NextUpdate)
                continue;

            docComp.NextUpdate = now + TimeSpan.FromSeconds(1);
            ChangePestilence(uid, docComp, docComp.PestilencePerSecond);
        }
    }

    private void OnMeleeHit(EntityUid uid, PlagueDoctorComponent comp, ref MeleeHitEvent args)
    {
        if (!args.IsHit)
            return;

        //доп. демедж
        foreach (var target in args.HitEntities)
        {
            if (!HasComp<MobStateComponent>(target))
                continue;

            if (HasComp<HumanoidAppearanceComponent>(target) && !_predator.IsVisibleByPredator(target, uid))
                continue;

            _audio.PlayPredicted(comp.HitSound, target, uid);
            _damageable.TryChangeDamage(target, comp.HitDamage, origin: uid);
        }

        ///зомбифицируем если в рейдже
        if (comp.State != PlagueDoctorState.Rage)
            return;
        foreach (var target in args.HitEntities)
        {
            if (HasComp<ZombieImmuneComponent>(target) || HasComp<ZombieImmuneComponent>(target) || HasComp<ZombieComponent>(target))
                continue;

            EnsureComp<ZombifyOnDeathComponent>(target);
        }
    }

    private void OnDisconnect(EntityUid uid, PlagueDoctorComponent comp, ResearchLinkDisconnectionEvent args)
    {
        if (comp.State != PlagueDoctorState.Safe)
            return;

        _ambient.SetSound(uid, comp.FreeAmbient);
    }

    private void OnResearchAttempt(EntityUid uid, PlagueDoctorComponent comp, ResearchAttemptEvent args)
    {
        if (comp.State == PlagueDoctorState.Safe)
        {
            _ambient.SetSound(uid, comp.CageAmbient);
            return;
        }
        args.Cancel();
    }

    private void OnMapInit(EntityUid uid, PlagueDoctorComponent comp, ref MapInitEvent args)
    {
        comp.ActionEnt = _actions.AddAction(uid, comp.ActionId);
        var fillPercentage = Math.Clamp(comp.CurrentPestilence / comp.MaxPestilence, 0f, 1f);
        var severity = (short)MathF.Round(fillPercentage * 5);
        _alerts.ShowAlert(uid, comp.PestilenceAlert, severity);
    }

    private void OnBeforeDamageChanged(EntityUid uid, PlagueDoctorComponent component, ref BeforeDamageChangedEvent args)
    {
        if (args.Origin == null)
            return;

        if (args.Origin == uid)
            return;

        if (HasComp<ZombieComponent>(args.Origin))
            args.Cancelled = true;
    }
    /// <summary>
    /// каждые 3 урона от кого-то повышают поветрие на 1 ед.
    /// </summary>
    private void OnDamageChange(EntityUid uid, PlagueDoctorComponent component, DamageChangedEvent args)
    {
        if (args.Origin == null || args.DamageDelta == null)
            return;

        if (!HasComp<ActorComponent>(args.Origin))
            return;

        var source = args.Origin.Value;
        var total = (float)args.DamageDelta.GetTotal();
        if (total <= 0 || source == uid)
            return;

        ChangePestilence(uid, component, total / 3f);
    }
    #region хирургия
    private void OnSurgeryAction(EntityUid uid, PlagueDoctorComponent comp, ref Surgery049Event args)
    {
        if (args.Handled)
            return;

        var target = args.Target;

        if (!_mob.IsDead(target))
        {
            Popup.PopupClient(Loc.GetString("archon049-surgery-target-not-dead", ("target", target)), target, uid, PopupType.Medium);
            return;
        }

        // нельзя оперировать одних и тех же
        if (TryComp<MetaDataComponent>(target, out var meta) && meta.EntityPrototype != null && comp.OperatedProtos.Contains(meta.EntityPrototype.ID))
        {
            Popup.PopupClient(Loc.GetString("archon049-surgery-target-was-surgery"), target, uid, PopupType.Medium);
            return;
        }


        var doAfterEventArgs = new DoAfterArgs(EntityManager, uid, comp.SurgeryDoAfterTime, new Surgery049DoAfterEvent(), eventTarget: uid, target: target)
        {
            DistanceThreshold = 2f,
            BreakOnMove = true,
            BreakOnDamage = true
        };

        if (!_doAfter.TryStartDoAfter(doAfterEventArgs))
            return;

        if (_timing.IsFirstTimePredicted)
            comp.Stream = _audio.PlayPredicted(comp.SurgerySound, uid, uid)?.Entity;

        args.Handled = true;
    }


    private void OnSurgeryDoAfter(EntityUid uid, PlagueDoctorComponent comp, Surgery049DoAfterEvent args)
    {
        if (_timing.IsFirstTimePredicted)
            comp.Stream = _audio.Stop(comp.Stream);

        if (args.Cancelled || args.Handled || args.Args.Target == null)
            return;

        ChangePestilence(uid, comp, comp.PestilencePerSurgery);
        MakeSurgery(uid, comp, args.Args.Target.Value);

        if (TryComp<ArchonComponent>(uid, out var archon))
            _archon.ExtractResearchPoints((uid, archon));

        if (TryComp<MetaDataComponent>(args.Args.Target, out var meta) && meta.EntityPrototype != null)
        {
            comp.OperatedProtos.Add(meta.EntityPrototype.ID);
            Dirty(uid, comp);
        }
        args.Handled = true;
    }
    #endregion
    /// <summary>
    /// управляем анимацией
    /// </summary>
    private void ProcessAnimation(EntityUid uid, PlagueDoctorComponent comp)
    {
        if (comp.State == PlagueDoctorState.Raging && _timing.CurTime >= comp.RagingAnimationEndAt)
            MakeRage(uid, comp);
    }

    public void ChangePestilence(EntityUid uid, PlagueDoctorComponent comp, float points)
    {
        // назад дороги нет
        if (comp.State != PlagueDoctorState.Safe)
            return;

        var newValue = comp.CurrentPestilence + points;
        newValue = Math.Clamp(newValue, 0, comp.MaxPestilence);

        if (newValue == comp.CurrentPestilence)
            return;

        comp.CurrentPestilence = newValue;
        var fillPercentage = Math.Clamp(comp.CurrentPestilence / comp.MaxPestilence, 0f, 1f);
        var severity = (short)MathF.Round(fillPercentage * 5);
        _alerts.UpdateAlert(uid, comp.PestilenceAlert, severity);
        if (newValue == comp.MaxPestilence)
            MakeRaging(uid, comp);

        Dirty(uid, comp);
    }

    protected virtual void MakeRaging(EntityUid uid, PlagueDoctorComponent comp)
    {
        EnsureComp<AdminFrozenComponent>(uid);
        comp.State = PlagueDoctorState.Raging;

        comp.RagingAnimationEndAt = _timing.CurTime + comp.RagingAnimationDuration;
        _appearance.SetData(uid, DamageVisualizerKeys.DamageUpdateGroups, PlagueDoctorState.Raging);

        _audio.PlayPredicted(comp.RagingSound, uid, uid);
        _alerts.ClearAlert(uid, comp.PestilenceAlert);
        _actions.RemoveAction(uid, comp.ActionEnt);
    }

    protected virtual void MakeRage(EntityUid uid, PlagueDoctorComponent comp)
    {
        RemComp<AdminFrozenComponent>(uid);
        _ambient.SetSound(uid, comp.RageAmbient);
        _appearance.SetData(uid, DamageVisualizerKeys.DamageUpdateGroups, PlagueDoctorState.Rage);
        comp.State = PlagueDoctorState.Rage;
        Dirty(uid, comp);
    }
    protected abstract void MakeSurgery(EntityUid uid, PlagueDoctorComponent comp, EntityUid target);
}
