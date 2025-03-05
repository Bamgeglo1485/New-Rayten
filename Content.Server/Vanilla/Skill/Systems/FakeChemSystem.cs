using Content.Shared.Chemistry.Components;
using Content.Shared.Vanilla.Skill;
using Robust.Shared.Random;

namespace Content.Server.Vanilla.Skill;

public sealed class FakeChemSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;    
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SolutionContainerVisualsComponent, MapInitEvent>(OnMapInit);
    }
    private void OnMapInit(EntityUid uid, SolutionContainerVisualsComponent component, MapInitEvent args)
    {
        if(TryComp<FakeChemComponent>(uid, out var FakeChem))
            return;

        FakeChem = EnsureComp<FakeChemComponent>(uid);
        FakeChem.FakeColor = new Color(_random.NextFloat(), _random.NextFloat(), _random.NextFloat(), 1.0f);
    }
}