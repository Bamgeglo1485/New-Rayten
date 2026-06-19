using Robust.Shared.Prototypes;
using Content.Shared.EntityEffects;
using Robust.Shared.GameObjects;
using Content.Shared.Vanilla.CompoundZ;

namespace Content.Shared.Vanilla.EntityEffects.Effects;

public sealed partial class RemoveSuperEntityEffectSystem : EntityEffectSystem<MetaDataComponent, RemoveSuper>
{
    protected override void Effect(Entity<MetaDataComponent> entity, ref EntityEffectEvent<RemoveSuper> args)
    {
        if (HasComp<SuperComponent>(entity))
            RemComp<SuperComponent>(entity);
    }
}

public sealed partial class RemoveSuper : EntityEffectBase<RemoveSuper>
{
    public override string EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => ("Убирает суперспособность");
}
