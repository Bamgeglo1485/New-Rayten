using Content.Server.Administration.Logs;
using Content.Server.SkillTrainer;
using Content.Shared.Damage;
using Content.Shared.Database;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Repairable;
using Content.Shared.Vanilla.Skill;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;

using SharedToolSystem = Content.Shared.Tools.Systems.SharedToolSystem;

namespace Content.Server.Repairable
{
    public sealed class RepairableSystem : SharedRepairableSystem
    {
        [Dependency] private readonly SharedToolSystem _toolSystem = default!;
        [Dependency] private readonly DamageableSystem _damageableSystem = default!;
        [Dependency] private readonly SharedPopupSystem _popup = default!;
        [Dependency] private readonly IAdminLogManager _adminLogger= default!;
        [Dependency] private readonly SharedAudioSystem _audio = default!;
        [Dependency] private readonly ServerSkillTrainerSystem _skillTrainerSystem = default!;
        public override void Initialize()
        {
            SubscribeLocalEvent<RepairableComponent, InteractUsingEvent>(Repair);
            SubscribeLocalEvent<RepairableComponent, RepairFinishedEvent>(OnRepairFinished);
        }

        private void OnRepairFinished(EntityUid uid, RepairableComponent component, RepairFinishedEvent args)
        {
            if (args.Cancelled)
                return;

            if (!EntityManager.TryGetComponent(uid, out DamageableComponent? damageable) || damageable.TotalDamage == 0)
                return;

            if (component.Damage != null)
            {
                var damageChanged = _damageableSystem.TryChangeDamage(uid, component.Damage, true, false, origin: args.User);
                _adminLogger.Add(LogType.Healed, $"{ToPrettyString(args.User):user} repaired {ToPrettyString(uid):target} by {damageChanged?.GetTotal()}");
            }

            else
            {
                // Repair all damage
                _damageableSystem.SetAllDamage(uid, damageable, 0);
                _adminLogger.Add(LogType.Healed, $"{ToPrettyString(args.User):user} repaired {ToPrettyString(uid):target} back to full health");
            }
            //vanilla-station-start
            if (TryComp<ActorComponent>(args.User, out var actor))
            {
                if (!EntityManager.TryGetComponent<SkillComponent>(args.User, out var skillComp))
                    skillComp = EnsureComp<SkillComponent>(args.User);

                if(_skillTrainerSystem.AddExperience(skillComp, skillType.Building, component.DoAfterDelay * 10))
                        _audio.PlayGlobal("/Audio/Vanilla/SkillSystem/levelup.ogg", actor.PlayerSession);

                RaiseNetworkEvent(new UpdateCharacterSkillsRequestEvent(), Filter.SinglePlayer(actor.PlayerSession));
            }
            //vanilla-station-end
            var str = Loc.GetString("comp-repairable-repair",
                ("target", uid),
                ("tool", args.Used!));
            _popup.PopupEntity(str, uid, args.User);

            var ev = new RepairedEvent((uid, component), args.User);
            RaiseLocalEvent(uid, ref ev);
        }

        public async void Repair(EntityUid uid, RepairableComponent component, InteractUsingEvent args)
        {
            if (args.Handled)
                return;

            // Only try repair the target if it is damaged
            if (!TryComp<DamageableComponent>(uid, out var damageable) || damageable.TotalDamage == 0)
                return;

            float delay = component.DoAfterDelay;

            // Add a penalty to how long it takes if the user is repairing itself
            if (args.User == args.Target)
            {
                if (!component.AllowSelfRepair)
                    return;

                delay *= component.SelfRepairPenalty;
            }
            //vanilla-station-start
            if(TryComp<SkillComponent>(args.User, out var SkillComponent))

                switch(SkillComponent.BuildingLevel){

                    case SkillLevel.None:
                        delay *= 9;
                        break;
                    case SkillLevel.Basic:
                        delay *= 6;
                        break;
                    case SkillLevel.Expert:
                        delay = 0.5f;
                        break;
                }

            else
                delay *= 9;
            //vanilla-station-end

            // Run the repairing doafter
            args.Handled = _toolSystem.UseTool(args.Used, args.User, uid, delay, component.QualityNeeded, new RepairFinishedEvent(), component.FuelCost);
        }
    }

    /// <summary>
    /// Event raised on an entity when its successfully repaired.
    /// </summary>
    /// <param name="Ent"></param>
    /// <param name="User"></param>
    [ByRefEvent]
    public readonly record struct RepairedEvent(Entity<RepairableComponent> Ent, EntityUid User);

}
