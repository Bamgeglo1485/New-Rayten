using Robust.Shared.Prototypes;
using Content.Shared.EntityEffects;
using Robust.Shared.GameObjects;
using Content.Shared.Vanilla.CompoundZ;

namespace Content.Shared.Vanilla.EntityEffects.Effects;

public sealed partial class MakeSuperEntityEffectSystem : EntityEffectSystem<MetaDataComponent, MakeSuper>
{

    protected override void Effect(Entity<MetaDataComponent> entity, ref EntityEffectEvent<MakeSuper> args)
    {
        if (!HasComp<SuperComponent>(entity))
            AddComp<SuperComponent>(entity);
    }
}

public sealed partial class MakeSuper : EntityEffectBase<MakeSuper>
{
    public override string EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => ("Даёт суперспособность");
}
