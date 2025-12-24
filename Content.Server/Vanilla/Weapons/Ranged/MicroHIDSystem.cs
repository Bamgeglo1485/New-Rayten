using Content.Server.Beam;
using Content.Shared.Vanilla.Weapons.Ranged;
using Content.Shared.Lightning;
namespace Content.Server.Vanilla.Weapons.Ranged;

public sealed partial class MicroHIDSystem : SharedMicroHIDSystem
{
    [Dependency] private readonly BeamSystem _beam = default!;
    [Dependency] private readonly SharedLightningSystem _light = default!;

    public override void Initialize()
    {
        base.Initialize();
    }
    public override void Update(float frameTime)
    {
        base.Update(frameTime);
    }
    public override void Shoot(EntityUid user, EntityUid target, string proto = "MicroHidLightning")
    {
        var spriteState = _light.LightningRandomizer();
        _beam.TryCreateBeam(user, target, proto, spriteState);
    }
}
