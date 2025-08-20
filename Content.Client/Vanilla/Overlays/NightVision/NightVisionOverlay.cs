using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Content.Shared.Vanilla.Entities.NightVision;

namespace Content.Client.Vanilla.NightVision;

public sealed class NightVisionOverlay : Overlay
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly ILightManager _lightManager = default!;

    public override bool RequestScreenTexture => true;
    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    private readonly ShaderInstance _greyscaleShader;
    private readonly ShaderInstance _circleMaskShader;

    public NightVisionOverlay()
    {
        IoCManager.InjectDependencies(this);
        _greyscaleShader = _prototypeManager.Index<ShaderPrototype>("GreyscaleFullscreen").InstanceUnique();
        _circleMaskShader = _prototypeManager.Index<ShaderPrototype>("CircleMask").InstanceUnique();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null)
            return;

        var playerEntity = _playerManager.LocalSession?.AttachedEntity;

        if (playerEntity == null)
            return;


        if (_entityManager.TryGetComponent<EyeComponent>(playerEntity, out var content))
        {
            _circleMaskShader?.SetParameter("Zoom", content.Zoom.X / 14); // Neh, but looks nice
        }

        _greyscaleShader?.SetParameter("SCREEN_TEXTURE", ScreenTexture);

        var worldHandle = args.WorldHandle;
        var viewport = args.WorldBounds;
        worldHandle.UseShader(_greyscaleShader);
        worldHandle.DrawRect(viewport, Color.Green);
        worldHandle.UseShader(_circleMaskShader);
        worldHandle.DrawRect(viewport, Color.Gray);
        worldHandle.UseShader(null);
    }
}
