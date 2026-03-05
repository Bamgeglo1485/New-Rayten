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
    [Dependency] private readonly IEyeManager _eyeManager = default!;
    [Dependency] private readonly EntityLookupSystem _entityLookup = default!;
    [Dependency] private readonly IUserInterfaceManager _userInterfaceManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly ExamineSystemShared _examineSystemShared = default!;

    public override void Initialize()
    {
        base.Initialize();
        _overlay.AddOverlay(new ShowNamesOverlay(EntityManager, _eyeManager, IoCManager.Resolve<IResourceCache>(), _entityLookup, _userInterfaceManager, _playerManager, _examineSystemShared));
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlay.RemoveOverlay<ShowNamesOverlay>();
    }
}
