using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Client.Overlays;

public sealed partial class VignetteOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> Shader = "Vignette";

    [Dependency] private IPrototypeManager _prototypeManager = default!;

    public float _outer_radius = 0.2f;
    public float _main_alpha = 0.3f;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    public override bool RequestScreenTexture => true;
    private readonly ShaderInstance _grainShader;

    public VignetteOverlay()
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
        _grainShader.SetParameter("OuterRadius", _outer_radius);
        _grainShader.SetParameter("MainAlpha", _main_alpha);
        _grainShader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        handle.UseShader(_grainShader);
        handle.DrawRect(args.WorldBounds, Color.White);
        handle.UseShader(null);
    }
}
