using Content.Shared.Damage;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;
using Content.Shared.Vanilla.Skill;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Explosion;

namespace Content.Server.Vanilla.Skill;

public sealed class ChemArmorSystem : EntitySystem
{
    const float EXPLOSIONSKILLARMOR = 0.6f;
    [Dependency] private readonly IPrototypeManager _protoManager = default!;    
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SkillComponent, DamageModifyEvent>(OnDamageModify);
        SubscribeLocalEvent<SkillComponent, GetExplosionResistanceEvent>(OnGetResistance);
    }

    private void OnDamageModify(EntityUid uid, SkillComponent component, DamageModifyEvent args)    
    {
        if (component.ChemistryLevel != SkillLevel.Expert) 
            return;


        ProtoId<DamageModifierSetPrototype> modifierSetId = "ChemExpert";
        var modifierSet = _protoManager.Index<DamageModifierSetPrototype>(modifierSetId);

        if (modifierSet != null)
        {
            args.Damage = DamageSpecifier.ApplyModifierSet(args.Damage, modifierSet);
        }
    }
    private void OnGetResistance(EntityUid uid, SkillComponent component, ref GetExplosionResistanceEvent args)
    {
        if (component.ChemistryLevel != SkillLevel.Expert) 
            return;
        args.DamageCoefficient *= EXPLOSIONSKILLARMOR;
    }
}