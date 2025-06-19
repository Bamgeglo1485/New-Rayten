using Content.Shared.Vanilla.Skill;
using Content.Shared.Strip.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Hands.Components;
namespace Content.Server.Vanilla.Skill;

public sealed class ServerSkillChangeListener : EntitySystem
{
    [Dependency] private readonly GunSkillsSystem _gunskill = default!;
    [Dependency] private readonly SharedGunSystem _gun = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SkillComponent, SkillLevelChangedEvent>(OnSkillLevelChanged);
    }

    private void OnSkillLevelChanged(EntityUid uid, SkillComponent component, SkillLevelChangedEvent args)
    {
        if (args.IsExp)
            return;

        switch (args.Skill)
        {
            case skillType.Crime:
                ReactOnCrimeLevelChanged(uid, component);
                break;
            case skillType.RangeWeapon:
                ReactOnRangeWeaponLevelChanged(uid, component);
                break;
        }
    }
    private void ReactOnRangeWeaponLevelChanged(EntityUid uid, SkillComponent component)
    {
        if (TryComp<HandsComponent>(uid, out var hands) &&
            hands.ActiveHand?.HeldEntity is { } weapon &&
            TryComp<UnskilledWeaponComponent>(weapon, out var unskilledComp))
        {
            _gunskill.UnskilledWeaponRefreshModifiers(component, unskilledComp);
            _gun.RefreshModifiers(weapon);
        }
    }
    private void ReactOnCrimeLevelChanged(EntityUid uid, SkillComponent component)
    {
        if (component.CrimeLevel == SkillLevel.None)
        {
            RemComp<AssComponent>(uid);
            RemComp<ThievingComponent>(uid);
        }
        if (component.CrimeLevel == SkillLevel.Basic)
        {
            RemComp<AssComponent>(uid);
            RemComp<ThievingComponent>(uid);
        }
        if (component.CrimeLevel == SkillLevel.Advanced)
        {
            AddComp<AssComponent>(uid);
            RemComp<ThievingComponent>(uid);
        }
        if (component.CrimeLevel == SkillLevel.Expert)
        {
            AddComp<ThievingComponent>(uid);
        }
    }

}
