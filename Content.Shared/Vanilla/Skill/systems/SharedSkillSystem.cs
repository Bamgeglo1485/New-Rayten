using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Damage.Systems;
using Content.Shared.Chemistry;
using Content.Shared.UserInterface;
using Content.Shared.Interaction;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Player;

namespace Content.Shared.Vanilla.Skill;

public abstract partial class SharedSkillSystem : EntitySystem
{
    [Dependency] protected readonly SharedAudioSystem Audio = default!;
    [Dependency] protected readonly IRobustRandom _Random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedGunSystem _gun = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;


    const int EXPERIENCEFROMSKILLPOINT = 600;
    const int EXPERIENCETONEWLVL = 600;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeAllEvent<UseSkillPointEvent>(UseSkillPoint);
        SubscribeLocalEvent<SkillComponent, ComponentRemove>(OnComponentRemoved);

        SubscribeLocalEvent<MeleeWeaponComponent, GetMeleeDamageEvent>(OnMeleeDamage);
        SubscribeLocalEvent<SkillComponent, SolutionScanEvent>(OnChemScan, after: [typeof(SolutionScannerSystem)]);
        SubscribeLocalEvent<SkillInvisibleComponent, GotEquippedHandEvent>(OnEquippedHand);

        SubscribeLocalEvent<RequiresSkillComponent, ActivatableUIOpenAttemptEvent>(OnActivate);//Открытие интерфейса
        SubscribeLocalEvent<RequiresSkillComponent, ItemSlotInsertAttemptEvent>(OnItemSlotInsertAttempt); //попытка вставить что-то
        SubscribeLocalEvent<RequiresSkillComponent, ItemSlotEjectAttemptEvent>(OnItemSlotEjectAttempt); //попытка вытащить что-то
        SubscribeLocalEvent<RequiresSkillToActivateInWorldComponent, ActivateInWorldEvent>(OnSkillCheckToActivateInWorld);//потом удалить когда-нибудь
        //амнезия
        SubscribeLocalEvent<SkillComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<SkillAmnesiaComponent, MapInitEvent>(OnAmnesiaInit);
        //оружие
        SubscribeLocalEvent<GunComponent, GotEquippedHandEvent>(OnHandPickUp);
        SubscribeLocalEvent<UnskilledWeaponComponent, GunRefreshModifiersEvent>(OnGunRefreshModifiers);
    }
    private void OnComponentRemoved(EntityUid uid, SkillComponent component, ref ComponentRemove args)
    {
        UpdateAllSystems(uid, component);
    }
    private void UseSkillPoint(UseSkillPointEvent msg, EntitySessionEventArgs args)
    {
        if (!args.SenderSession.AttachedEntity.HasValue)
            return;

        var entity = args.SenderSession.AttachedEntity.Value;

        if (!TryComp<SkillComponent>(entity, out var skillComp) || skillComp.SkillPoints < 1)
            return;

        if (AddExperience((entity, skillComp), msg.skill, EXPERIENCEFROMSKILLPOINT))
        {
            skillComp.SkillPoints--;
            Dirty(entity, skillComp);
        }
    }

    public bool AddExperience(Entity<SkillComponent> ent, SkillType skillType, int experienceAmount)
    {
        if (experienceAmount <= 0)
            return false;

        var comp = ent.Comp;

        var exp = comp.SkillExps.GetValueOrDefault(skillType) + experienceAmount;
        var threshold = EXPERIENCETONEWLVL;

        switch (skillType.GetKind())
        {
            case SkillKind.Easy:
                {
                    // Уже изучен
                    if (comp.EasySkills.Contains(skillType))
                        return false;

                    if (exp >= threshold)
                    {
                        comp.EasySkills.Add(skillType);
                        comp.SkillExps[skillType] = exp - threshold;
                        Audio.PlayGlobal(comp.LvlUpSound, Filter.Empty().FromEntities(ent.Owner), false);
                    }
                    else
                    {
                        comp.SkillExps[skillType] = exp;
                    }

                    break;
                }

            case SkillKind.Basic:
                {
                    var level = comp.BasicSkills.GetValueOrDefault(skillType, SkillLevel.None);

                    // Максимальный уровень
                    if (level == SkillLevel.Expert)
                        return false;

                    if (exp >= threshold)
                    {
                        comp.BasicSkills[skillType] = level + 1;
                        comp.SkillExps[skillType] = exp - threshold;
                        Audio.PlayGlobal(comp.LvlUpSound, Filter.Empty().FromEntities(ent.Owner), false);
                    }
                    else
                    {
                        comp.SkillExps[skillType] = exp;
                    }

                    break;
                }
        }
        Dirty(ent);
        UpdateAllSystems(ent.Owner, ent.Comp);
        return true;
    }

    public virtual void UpdateAllSystems(EntityUid uid, SkillComponent component)
    {
        UpdateGun(uid, component);
    }

    #region help
    public void FuckSkills(EntityUid uid, SkillComponent? component = null)
    {
        if (!Resolve(uid, ref component, false))
            return;

        foreach (var skillType in Enum.GetValues<SkillType>())
        {
            switch (skillType.GetKind())
            {
                case SkillKind.Basic:
                    component.BasicSkills[skillType] = SkillLevel.Expert;
                    break;

                case SkillKind.Easy:
                    component.EasySkills.Add(skillType);
                    break;
            }
        }
        Dirty(uid, component);
        UpdateAllSystems(uid, component);
    }
    public bool TryGetSkill(
        EntityUid uid,
        SkillType skill,
        out bool hasEasySkill,
        out SkillLevel level,
        SkillComponent? component = null)
    {
        hasEasySkill = false;
        level = SkillLevel.None;

        if (!Resolve(uid, ref component, false))
            return false;

        switch (skill.GetKind())
        {
            case SkillKind.Easy:
                hasEasySkill = component.EasySkills.Contains(skill);
                return true;

            case SkillKind.Basic:
                level = component.BasicSkills.GetValueOrDefault(skill, SkillLevel.None);
                return true;
        }

        return false;
    }
    #endregion
}