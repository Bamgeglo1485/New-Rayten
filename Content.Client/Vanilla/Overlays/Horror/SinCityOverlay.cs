using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Client.Overlays;

public sealed partial class SinCityOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> Shader = "SinCity";

    [Dependency] private IPrototypeManager _prototypeManager = default!;

    public float Saturation = 0.65f;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    public override bool RequestScreenTexture => true;
    private readonly ShaderInstance _grainShader;

    public SinCityOverlay()
    {
        IoCManager.InjectDependencies(this);
        _grainShader = _prototypeManager.Index(Shader).InstanceUnique();
        ZIndex = 10;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null)
            return;

        var handle = args.WorldHandle;
        _grainShader.SetParameter("EnvironmentSaturation", Saturation);
        _grainShader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        handle.UseShader(_grainShader);
        handle.DrawRect(args.WorldBounds, Color.White);
        handle.UseShader(null);
    }
}
