using Content.Shared.Chemistry.Components;
using Content.Shared.Vanilla.Skill;
using Robust.Shared.Random;

namespace Content.Server.Vanilla.Skill;

public sealed partial class SkillSystem : SharedSkillSystem
{
    private void OnMapInit(EntityUid uid, SolutionContainerVisualsComponent component, MapInitEvent args)
    {
        var fakeChemComp = EnsureComp<FakeChemComponent>(uid);
        fakeChemComp.FakeColor = new Color(_Random.NextFloat(), _Random.NextFloat(), _Random.NextFloat(), 1.0f);
    }
}