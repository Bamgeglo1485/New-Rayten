using Content.Shared.UserInterface;
using Content.Shared.Interaction;
using Content.Shared.Containers.ItemSlots;

namespace Content.Shared.Vanilla.Skill;

public abstract partial class SharedSkillSystem : EntitySystem
{
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


    public bool HasRequiredSkill(EntityUid uid, SkillType skill, SkillLevel? requiredLvl = null, bool WithBeep = true, SkillComponent? component = null, bool ServerOnly = false)
    {
        if (!Resolve(uid, ref component, false))
            return false;

        if (!TryGetSkill(uid, skill, out var hasEasy, out var level, component))
            return false;

        switch (skill.GetKind())
        {
            case SkillKind.Easy:
                if (hasEasy)
                    return true;
                break;

            case SkillKind.Basic:
                if (requiredLvl is null)
                {
                    Log.Error($"Для основного навыка {skill} не указан requiredLvl");
                    return false;
                }

                if (level >= requiredLvl.Value)
                    return true;
                break;
        }
        if (ServerOnly)
        {
            if (WithBeep)
            {
                _popup.PopupEntity(Loc.GetString("Skill-issue-message-unskilled", ("skill", skill.ToString())), uid, uid);
                Audio.PlayGlobal(component.UnSkillSound, uid);
            }
            return false;
        }
        if (WithBeep)
        {
            _popup.PopupCursor(Loc.GetString("Skill-issue-message-unskilled", ("skill", skill.ToString())));
            Audio.PlayLocal(component.UnSkillSound, uid, uid);
        }
        //else

        return false;
    }
    public bool HasRequiredSkill(EntityUid uid, RequiresSkillComponent reqcomp, bool WithBeep = true, SkillComponent? component = null, bool ServerOnly = false)
    {
        if (!Resolve(uid, ref component, false))
            return false;

        // Проверка basic-навыков
        foreach (var (skill, requiredLevel) in reqcomp.BasicSkills)
        {
            if (!HasRequiredSkill(uid, skill, requiredLevel, WithBeep, component, ServerOnly))
                return false;
        }

        // Проверка easy-навыков
        foreach (var skill in reqcomp.EasySkills)
        {
            if (!HasRequiredSkill(uid, skill, null, WithBeep, component, ServerOnly))
                return false;
        }

        return true;
    }
}