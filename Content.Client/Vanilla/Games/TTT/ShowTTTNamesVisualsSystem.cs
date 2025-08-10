using Content.Client.Vanilla.Overlays;
using Content.Shared.Examine;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.ResourceManagement;
using Robust.Client.Player;

namespace Content.Client.Vanilla.Games.TTT;

public sealed class ShowTTTNamesVisualsSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlay = default!;
    [Dependency] private readonly IEyeManager eyeManager = default!;
    [Dependency] private readonly EntityLookupSystem entityLookup = default!;
    [Dependency] private readonly IUserInterfaceManager userInterfaceManager = default!;
    [Dependency] private readonly IPlayerManager playerManager = default!;
    [Dependency] private readonly ExamineSystemShared examineSystemShared = default!;

    public override void Initialize()
    {
        base.Initialize();
        _overlay.AddOverlay(new ShowTTTNamesOverlay(EntityManager, eyeManager, IoCManager.Resolve<IResourceCache>(), entityLookup, userInterfaceManager, playerManager, examineSystemShared));
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlay.RemoveOverlay<ShowTTTNamesOverlay>();
    }
}