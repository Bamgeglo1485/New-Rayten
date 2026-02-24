using Content.Shared.Vanilla.Archon.OldMan;
using Robust.Shared.Prototypes;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using System.Numerics;
namespace Content.Client.Vanilla.Archon.OldMan;

public sealed class PuddleMaskOverlay : Overlay
{
    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowEntities;
    private static readonly ProtoId<ShaderPrototype> StencilMaskShader = "StencilMask";
    private static readonly ProtoId<ShaderPrototype> StencilEqualDrawShader = "StencilEqualDraw";

    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IEntityManager _entManager = default!;
    private readonly SharedTransformSystem _xform;

    public PuddleMaskOverlay()
    {
        IoCManager.InjectDependencies(this);
        _xform = _entManager.System<SharedTransformSystem>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var worldHandle = args.WorldHandle;
        worldHandle.UseShader(_proto.Index(StencilMaskShader).Instance());

        var puddles = _entManager.EntityQueryEnumerator<PDlushaComponent>();
        while (puddles.MoveNext(out var uid, out _))
        {
            var xform = _xform.GetWorldMatrix(uid);
            worldHandle.SetTransform(xform);

            worldHandle.DrawRect(Box2.UnitCentered, Color.White);
        }

        worldHandle.UseShader(null);

        worldHandle.UseShader(_proto.Index(StencilEqualDrawShader).Instance());
        worldHandle.UseShader(null);
        worldHandle.SetTransform(Matrix3x2.Identity);
    }
}
