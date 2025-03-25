using Content.Shared.UserInterface;
using Content.Shared.Interaction;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Interaction.Events;
using Content.Shared.Hands;
using Content.Shared.Popups;
using Robust.Shared.Network;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Shared.Vanilla.Skill;

public sealed class RequiresSkillSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RequiresSkillComponent, ActivatableUIOpenAttemptEvent>(OnActivate);//Открытие интерфейса
        SubscribeLocalEvent<RequiresSkillComponent, ItemSlotInsertAttemptEvent>(OnItemSlotInsertAttempt); //попытка вставить что-то
        SubscribeLocalEvent<RequiresSkillComponent, ItemSlotEjectAttemptEvent>(OnItemSlotEjectAttempt); //попытка вытащить что-то
        SubscribeLocalEvent<RequiresSkillToActivateInWorldComponent, ActivateInWorldEvent >(OnSkillCheckToActivateInWorld);
    }
    private void OnActivate(EntityUid uid, RequiresSkillComponent component, ref ActivatableUIOpenAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        var hasSkills = HasRequiredSkills(args.User, component, popup: _timing.IsFirstTimePredicted );

        var hasCraftSkills = !component.NeedCraftableSkills || HasRequiredSkillsForCraft(args.User, component);

        if (hasSkills && hasCraftSkills)
            return;

        args.Cancel();
    }

    private void OnSkillCheckToActivateInWorld(EntityUid uid, RequiresSkillToActivateInWorldComponent component, ref ActivateInWorldEvent args)
    {
        if (args.Handled)
            return;
            
        if(!EntityManager.TryGetComponent<RequiresSkillComponent>(uid, out var Reqcomponent))
            return;

        if(HasRequiredSkills(args.User, Reqcomponent, popup: _timing.IsFirstTimePredicted ))
            return;
            
        args.Handled = true;
    }

    private void OnItemSlotInsertAttempt(EntityUid uid, RequiresSkillComponent component, ref ItemSlotInsertAttemptEvent args)
    {
        if (uid != args.SlotEntity || args.Cancelled || args.User == null)
            return;

        var hasSkills = HasRequiredSkills(args.User.Value, component, popup: _timing.IsFirstTimePredicted );

        var hasCraftSkills = !component.NeedCraftableSkills || HasRequiredSkillsForCraft(args.User.Value, component);

        if (hasSkills && hasCraftSkills)
            return;

        args.Cancelled = true;
    }
    private void OnItemSlotEjectAttempt(EntityUid uid, RequiresSkillComponent component, ref ItemSlotEjectAttemptEvent args)
    {
        if (uid != args.SlotEntity || args.Cancelled || args.User == null)
            return;
            
        var hasSkills = HasRequiredSkills(args.User.Value, component, popup: _timing.IsFirstTimePredicted );

        var hasCraftSkills = !component.NeedCraftableSkills || HasRequiredSkillsForCraft(args.User.Value, component);

        if (hasSkills && hasCraftSkills)
            return;
            
        args.Cancelled = true;
    }
    public bool HasRequiredSkills(EntityUid user, RequiresSkillComponent component, bool popup = true)
    {
        if(!TryComp<SkillComponent>(user, out var skill))
            skill = EnsureComp<SkillComponent>(user);

        // Проверка уровня химии
        if (!HasSkillLevel(user, component.RequiresChemistryLevel, skillComponent => skillComponent.ChemistryLevel))
        {
            if(popup)
            {
                _popup.PopupCursor(Loc.GetString("Skill-issue-message-chemistry-unskilled", ("lvl", (int)component.RequiresChemistryLevel)));
                if (_net.IsClient) _audio.PlayGlobal("/Audio/Vanilla/SkillSystem/meep-merp.ogg", Filter.Local(), false);
            }

            return false;
        }
        // Проверка уровня медицины
        if (!HasSkillLevel(user, component.RequiresMedicineLevel, skillComponent => skillComponent.MedicineLevel))
        {
            if(popup)
            {
                _popup.PopupCursor(Loc.GetString("Skill-issue-message-medicine-unskilled", ("lvl", (int)component.RequiresMedicineLevel)));
                if (_net.IsClient) _audio.PlayGlobal("/Audio/Vanilla/SkillSystem/meep-merp.ogg", Filter.Local(), false);
            }
            return false;
        }
        // Проверка уровня исследования
        if (!HasSkillLevel(user, component.RequiresResearchLevel, skillComponent => skillComponent.ResearchLevel))
        {
            if(popup)
            {
                _popup.PopupCursor(Loc.GetString("Skill-issue-message-research-unskilled", ("lvl", (int)component.RequiresResearchLevel)));
                if (_net.IsClient) _audio.PlayGlobal("/Audio/Vanilla/SkillSystem/meep-merp.ogg", Filter.Local(), false);
            }
            return false;
        }
        // Проверка уровня Инженерии
        if (!HasSkillLevel(user, component.RequiresEngineeringLevel, skillComponent => skillComponent.EngineeringLevel))
        {
            if(popup)
            {
                _popup.PopupCursor(Loc.GetString("Skill-issue-message-engineering-unskilled", ("lvl", (int)component.RequiresEngineeringLevel)));
                if (_net.IsClient) _audio.PlayGlobal("/Audio/Vanilla/SkillSystem/meep-merp.ogg", Filter.Local(), false);
            }
            return false;
        }
        // Проверка уровня пилотирования
        if (!HasEasySkill(user, component.RequiresPiloting, skillComponent => skillComponent.Piloting))
        {
            if(popup)
            {
                _popup.PopupCursor(Loc.GetString("Skill-issue-easyskill-message-piloting-unskilled"));
                if (_net.IsClient) _audio.PlayGlobal("/Audio/Vanilla/SkillSystem/meep-merp.ogg", Filter.Local(), false);
            }
            return false;
        }
        // Проверка уровня муз. инструментов
        if (!HasEasySkill(user, component.RequiresMusInstruments, skillComponent => skillComponent.MusInstruments))
        {
            if(popup)
                _popup.PopupCursor(Loc.GetString("Skill-issue-easyskill-message-musinstruments-unskilled"));
            return false;
        }
        // Проверка уровня ботаники
        if (!HasEasySkill(user, component.RequiresBotany, skillComponent => skillComponent.Botany))
        {
            if(popup)
            {
                _popup.PopupCursor(Loc.GetString("Skill-issue-easyskill-message-botany-unskilled"));
                if (_net.IsClient) _audio.PlayGlobal("/Audio/Vanilla/SkillSystem/meep-merp.ogg", Filter.Local(), false);
            }
            return false;
        }
        // Проверка уровня Атмосферы
        if (!HasEasySkill(user, component.RequiresAtmosphere, skillComponent => skillComponent.Atmosphere))
        {
            if(popup)
            {
                _popup.PopupCursor(Loc.GetString("Skill-issue-easyskill-message-atmosphere-unskilled"));
                if (_net.IsClient) _audio.PlayGlobal("/Audio/Vanilla/SkillSystem/meep-merp.ogg", Filter.Local(), false);
            }
            return false;
        }
        return true;
    }
    public bool HasAnyOfRequiredSkills(EntityUid user, RequiresSkillComponent component)
    {
        // Проверка уровня химии
        if (component.RequiresChemistryLevel!=0 && HasSkillLevel(user, component.RequiresChemistryLevel, skillComponent => skillComponent.ChemistryLevel)){
            return true;
        }
        // Проверка уровня медицины
        if (component.RequiresMedicineLevel!=0 && HasSkillLevel(user, component.RequiresMedicineLevel, skillComponent => skillComponent.MedicineLevel)){
            return true;
        }
        // Проверка уровня исследования
        if (component.RequiresResearchLevel!=0 && HasSkillLevel(user, component.RequiresResearchLevel, skillComponent => skillComponent.ResearchLevel)){
            return true;
        }
        // Проверка уровня Инженерии
        if (component.RequiresEngineeringLevel!=0 && HasSkillLevel(user, component.RequiresEngineeringLevel, skillComponent => skillComponent.EngineeringLevel)){
            return true;
        }
        // Проверка уровня пилотирования
        if (component.RequiresPiloting && HasEasySkill(user, component.RequiresPiloting, skillComponent => skillComponent.Piloting)){
            return true;
        }
        // Проверка уровня муз. инструментов
        if (component.RequiresMusInstruments && HasEasySkill(user, component.RequiresMusInstruments, skillComponent => skillComponent.MusInstruments)){
            return true;
        }
        // Проверка уровня ботаники
        if (component.RequiresBotany && HasEasySkill(user, component.RequiresBotany, skillComponent => skillComponent.Botany)){
            return true;
        }
        // Проверка уровня атмосферы
        if (component.RequiresAtmosphere && HasEasySkill(user, component.RequiresAtmosphere, skillComponent => skillComponent.Atmosphere)){
            return true;
        }
        return false;
    }

    public bool HasRequiredSkillsForCraft(EntityUid user, RequiresSkillComponent component, bool popup = false)
    {
        // Проверка уровня Инженерии
        if (!HasSkillLevel(user, component.RequiresEngineeringLevel, skillComponent => skillComponent.EngineeringLevel)){
            if(popup)
            {
                _popup.PopupCursor(Loc.GetString("Skill-issue-message-engineering-unskilled", ("lvl", (int)component.RequiresEngineeringLevel)));
                if (_net.IsClient) _audio.PlayGlobal("/Audio/Vanilla/SkillSystem/meep-merp.ogg", Filter.Local(), false);
            }

            return false;
        }
        // Проверка уровня Строительства
        if (!HasSkillLevel(user, component.RequiresBuildingLevel, skillComponent => skillComponent.BuildingLevel)){
            if(popup)
            {
                _popup.PopupCursor(Loc.GetString("Skill-issue-message-building-unskilled", ("lvl", (int)component.RequiresBuildingLevel)));
                if (_net.IsClient) _audio.PlayGlobal("/Audio/Vanilla/SkillSystem/meep-merp.ogg", Filter.Local(), false);
            }

            return false;
        }
        // Проверка уровня атмосферы
        if (!HasEasySkill(user, component.RequiresAtmosphere, skillComponent => skillComponent.Atmosphere)){
            if(popup)
            {
                _popup.PopupCursor(Loc.GetString("Skill-issue-easyskill-message-atmosphere-unskilled"));
                if (_net.IsClient) _audio.PlayGlobal("/Audio/Vanilla/SkillSystem/meep-merp.ogg", Filter.Local(), false);
            }
            return false;
        }  
        return true;
    }
    
    public bool HasSkillLevel(EntityUid user, SkillLevel requiredLevel, Func<SkillComponent, SkillLevel> skillSelector)
    {
        return TryComp<SkillComponent>(user, out var skillComponent) && skillSelector(skillComponent) >= requiredLevel;
    }

    public bool HasEasySkill(EntityUid user, bool requiredEasySkill, Func<SkillComponent, bool> skillSelector)
    {   
        if(!requiredEasySkill)
            return true;

        return TryComp<SkillComponent>(user, out var skillComponent) && skillSelector(skillComponent) == requiredEasySkill;
    }

    
}