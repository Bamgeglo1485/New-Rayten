using Content.Shared.UserInterface;
using Content.Shared.Interaction;
using Content.Shared.Vanilla.Skill;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Interaction.Events;
using Content.Server.Popups;
using Content.Shared.Chemistry.Components;
using Content.Shared.Hands;
using ActivatableUISystem = Content.Shared.UserInterface.ActivatableUISystem;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;

namespace Content.Server.Vanilla.Skill;

public sealed class RequiresSkillSystem : SharedRequiresSkillSystem
{
    [Dependency] private readonly ActivatableUISystem _activatableUI = default!;
    [Dependency] private readonly PopupSystem _popupSystem = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    protected override void OnActivate(EntityUid uid, RequiresSkillComponent component, ref ActivatableUIOpenAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        var hasSkills = HasRequiredSkills(args.User, component);

        var hasCraftSkills = !component.NeedCraftableSkills || HasRequiredSkillsForCraft(args.User, component);

        if (hasSkills && hasCraftSkills)
            return;

        args.Cancel();
    }

    protected override void OnSkillCheckToActivateInWorld(EntityUid uid, RequiresSkillToActivateInWorldComponent component, ref ActivateInWorldEvent args)
    {
        if (args.Handled)
            return;
        if(!EntityManager.TryGetComponent<RequiresSkillComponent>(uid, out var Reqcomponent))
            return;
        if(HasRequiredSkills(args.User, Reqcomponent))
            return;
            
        args.Handled = true;
    }

    protected override void OnItemSlotInsertAttempt(EntityUid uid, RequiresSkillComponent component, ref ItemSlotInsertAttemptEvent args)
    {
        if (uid != args.SlotEntity || args.Cancelled || args.User == null)
            return;

        var hasSkills = HasRequiredSkills(args.User.Value, component);

        var hasCraftSkills = !component.NeedCraftableSkills || HasRequiredSkillsForCraft(args.User.Value, component);

        if (hasSkills && hasCraftSkills)
            return;

        args.Cancelled = true;
    }
    protected override void OnItemSlotEjectAttempt(EntityUid uid, RequiresSkillComponent component, ref ItemSlotEjectAttemptEvent args)
    {
        if (uid != args.SlotEntity || args.Cancelled || args.User == null)
            return;

        var hasSkills = HasRequiredSkills(args.User.Value, component);

        var hasCraftSkills = !component.NeedCraftableSkills || HasRequiredSkillsForCraft(args.User.Value, component);

        if (hasSkills && hasCraftSkills)
            return;
            
        args.Cancelled = true;
    }

    public bool HasRequiredSkills(EntityUid user, RequiresSkillComponent component, bool popup, skillType? skillignore = null)
    {
        if(!TryComp<SkillComponent>(user, out var skill))
            skill = EnsureComp<SkillComponent>(user);

        if (!TryComp<ActorComponent>(user, out var actor))
            return false;
        var session = actor.PlayerSession;

        // Проверка уровня химии
        if (skillignore != skillType.Chemistry && !HasSkillLevel(user, component.RequiresChemistryLevel, skillComponent => skillComponent.ChemistryLevel)){
            if(popup)
            {
                _audio.PlayGlobal("/Audio/Vanilla/SkillSystem/meep-merp.ogg", session);
                _popupSystem.PopupEntity(Loc.GetString("Skill-issue-message-chemistry-unskilled", ("lvl", (int)component.RequiresChemistryLevel)), user, user);
            }

            return false;
        }
        // Проверка уровня медицины
        if (skillignore != skillType.Medicine && !HasSkillLevel(user, component.RequiresMedicineLevel, skillComponent => skillComponent.MedicineLevel)){
            if(popup)
            {
                _audio.PlayGlobal("/Audio/Vanilla/SkillSystem/meep-merp.ogg", session);
                _popupSystem.PopupEntity(Loc.GetString("Skill-issue-message-medicine-unskilled", ("lvl", (int)component.RequiresMedicineLevel)), user, user);
            }

            return false;
        }
        // Проверка уровня исследования
        if (skillignore != skillType.Research && !HasSkillLevel(user, component.RequiresResearchLevel, skillComponent => skillComponent.ResearchLevel)){
            if(popup)
            {
                _audio.PlayGlobal("/Audio/Vanilla/SkillSystem/meep-merp.ogg", session);
                _popupSystem.PopupEntity(Loc.GetString("Skill-issue-message-research-unskilled", ("lvl", (int)component.RequiresResearchLevel)), user, user);
            }

            return false;
        }
        // Проверка уровня инженерии
        if (skillignore != skillType.Engineering && !HasSkillLevel(user, component.RequiresEngineeringLevel, skillComponent => skillComponent.EngineeringLevel)){
            if(popup)
            {
                _audio.PlayGlobal("/Audio/Vanilla/SkillSystem/meep-merp.ogg", session);
                _popupSystem.PopupEntity(Loc.GetString("Skill-issue-message-engineering-unskilled", ("lvl", (int)component.RequiresEngineeringLevel)), user, user);
            }

            return false;
        }
        // Проверка уровня пилотирования
        if (skillignore != skillType.Piloting && !HasEasySkill(user, component.RequiresPiloting, skillComponent => skillComponent.Piloting)){
            if(popup)
            {
                _audio.PlayGlobal("/Audio/Vanilla/SkillSystem/meep-merp.ogg", session);
                _popupSystem.PopupEntity(Loc.GetString("Skill-issue-easyskill-message-piloting-unskilled"), user, user);
            }
            return false;
        }
        // Проверка уровня ботаники
        if (skillignore != skillType.Botany && !HasEasySkill(user, component.RequiresBotany, skillComponent => skillComponent.Botany)){
            if(popup)
            {
                _audio.PlayGlobal("/Audio/Vanilla/SkillSystem/meep-merp.ogg", session);
                _popupSystem.PopupEntity(Loc.GetString("Skill-issue-easyskill-message-botany-unskilled"), user, user);
            }
            return false;
        }
        // Проверка уровня муз. инструментов
        if (skillignore != skillType.MusInstruments && !HasEasySkill(user, component.RequiresMusInstruments, skillComponent => skillComponent.MusInstruments)){
            if(popup)
            {
                _audio.PlayGlobal("/Audio/Vanilla/SkillSystem/meep-merp.ogg", session);
                _popupSystem.PopupEntity(Loc.GetString("Skill-issue-easyskill-message-musinstruments-unskilled"), user, user);
            }
            return false;
        }
        // Проверка уровня воровства
        if (skillignore != skillType.Thief && !HasEasySkill(user, component.RequiresThief, skillComponent => skillComponent.Thief)){
            if(popup)
            {
                _audio.PlayGlobal("/Audio/Vanilla/SkillSystem/meep-merp.ogg", session);
                _popupSystem.PopupEntity(Loc.GetString("Skill-issue-easyskill-message-thief-unskilled"), user, user);
            }
            return false;
        }

        
        return true;
    }

    public bool HasRequiredSkillsForCraft(EntityUid user, RequiresSkillComponent component, bool popup)
    {
        if(!TryComp<SkillComponent>(user, out var skill))
            skill = EnsureComp<SkillComponent>(user);

        if (!TryComp<ActorComponent>(user, out var actor))
            return false;
        var session = actor.PlayerSession;

        // Проверка уровня Приборостроения
        if (!HasSkillLevel(user, component.RequiresInstrumentationLevel, skillComponent => skillComponent.InstrumentationLevel)){
            if(popup)
            {
                _audio.PlayGlobal("/Audio/Vanilla/SkillSystem/meep-merp.ogg", session);
                _popupSystem.PopupEntity(Loc.GetString("Skill-issue-message-instrumentation-unskilled", ("lvl", (int)component.RequiresInstrumentationLevel)), user, user);
            }
            return false;
        }
        // Проверка уровня Строительства
        if (!HasSkillLevel(user, component.RequiresBuildingLevel, skillComponent => skillComponent.BuildingLevel)){
            if(popup)
            {
                _audio.PlayGlobal("/Audio/Vanilla/SkillSystem/meep-merp.ogg", session);
                _popupSystem.PopupEntity(Loc.GetString("Skill-issue-message-building-unskilled", ("lvl", (int)component.RequiresBuildingLevel)), user, user);
            }
            return false;
        }
        return true;
    }

}