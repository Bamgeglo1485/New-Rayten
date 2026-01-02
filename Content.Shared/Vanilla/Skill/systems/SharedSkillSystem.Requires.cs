using Content.Shared.Popups;
using Content.Shared.UserInterface;
using Content.Shared.Interaction;
using Content.Shared.Containers.ItemSlots;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Shared.Vanilla.Skill;

public sealed partial class SharedSkillSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private void OnActivate(EntityUid uid, RequiresSkillComponent component, ref ActivatableUIOpenAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (HasRequiredSkill(args.User, component, WithBeep: _timing.IsFirstTimePredicted))
            return;

        args.Cancel();
    }

    private void OnSkillCheckToActivateInWorld(EntityUid uid, RequiresSkillToActivateInWorldComponent component, ref ActivateInWorldEvent args)
    {
        if (args.Handled)
            return;

        if (!EntityManager.TryGetComponent<RequiresSkillComponent>(uid, out var Reqcomponent))
            return;

        if (HasRequiredSkill(args.User, Reqcomponent, WithBeep: _timing.IsFirstTimePredicted))
            return;

        args.Handled = true;
    }

    private void OnItemSlotInsertAttempt(EntityUid uid, RequiresSkillComponent component, ref ItemSlotInsertAttemptEvent args)
    {
        if (uid != args.SlotEntity || args.Cancelled || args.User == null)
            return;

        if (HasRequiredSkill(args.User.Value, component, WithBeep: _timing.IsFirstTimePredicted))
            return;

        args.Cancelled = true;
    }

    private void OnItemSlotEjectAttempt(EntityUid uid, RequiresSkillComponent component, ref ItemSlotEjectAttemptEvent args)
    {
        if (uid != args.SlotEntity || args.Cancelled || args.User == null)
            return;

        if (HasRequiredSkill(args.User.Value, component, WithBeep: _timing.IsFirstTimePredicted))
            return;

        args.Cancelled = true;
    }

    public bool HasRequiredSkill(
        EntityUid uid,
        SkillType skill,
        SkillLevel? requiredLvl = null,
        bool WithBeep = true,
        SkillComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return false;

        // BASIC skill
        if (requiredLvl.HasValue)
        {
            if (IsEasySkill(skill))
            {
                Log.Error($"Передан {skill}, который является лёгким навыком!");
                return false;
            }

            var lvl = component.BasicSkills.GetValueOrDefault(skill, SkillLevel.None);
            if (lvl >= requiredLvl.Value)
                return true;

            if (WithBeep)
            {
                _popup.PopupCursor(Loc.GetString("Skill-issue-message-basicskill-unskilled", ("skill", skill), ("lvl", (int)requiredLvl.Value)));
                _audio.PlayLocal(component.UnSkillSound, uid, uid);
            }
            return false;
        }

        // EASY skill
        if (!IsEasySkill(skill))
        {
            Log.Error($"Передан {skill}, который не является лёгким навыком!");
            return false;
        }

        if (component.EasySkills.Contains(skill))
            return true;

        if (WithBeep)
        {
            _popup.PopupCursor(Loc.GetString("Skill-issue-message-easyskill-unskilled", ("skill", skill)));
            _audio.PlayLocal(component.UnSkillSound, uid, uid);
        }
        return false;
    }

    public bool HasRequiredSkill(EntityUid uid, RequiresSkillComponent reqcomp, bool WithBeep = true, SkillComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return false;

        // Проверка basic-навыков
        foreach (var (skill, requiredLevel) in reqcomp.BasicSkills)
        {
            if (!HasRequiredSkill(uid, skill, requiredLevel, WithBeep, component))
                return false;
        }

        // Проверка easy-навыков
        foreach (var skill in reqcomp.EasySkills)
        {
            if (!HasRequiredSkill(uid, skill, null, WithBeep, component))
                return false;
        }

        return true;
    }
}