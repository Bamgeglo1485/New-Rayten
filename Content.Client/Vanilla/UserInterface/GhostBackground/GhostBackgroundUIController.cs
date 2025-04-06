using Content.Shared.Vanilla.Background;
using Content.Client.Vanilla.Background;
using static Content.Client.Vanilla.Background.BackgroundSystem;
using Content.Client.UserInterface.Controls;
using Content.Client.Vanilla.UserInterface.GhostBackground.window;
using Content.Client.Vanilla.UserInterface.Background;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface;
using Robust.Shared.Utility;
using Content.Client.Gameplay;
using Robust.Shared.Prototypes;
using Robust.Client.Player;
using Robust.Shared.Player;
using JetBrains.Annotations;

namespace Content.Client.Vanilla.UserInterface.GhostBackground;

[UsedImplicitly]
public sealed class GhostBackgroundUIController : UIController, IOnStateEntered<GameplayState>, IOnStateExited<GameplayState>, IOnSystemChanged<BackgroundSystem>
{
    private GhostBackgroundWindow? _window;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly ILogManager _logMan = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [UISystemDependency] private readonly BackgroundSystem _backGroundSystem = default!;
    private ISawmill _sawmill = default!;
    public override void Initialize()
    {
        base.Initialize();
        _sawmill = _logMan.GetSawmill("ПРЕДЫСТОРИЯ");
    }

    public void createbackground(ProtoId<BackgroundGroupPrototype> BackgroundGroupID)
    {
        if(_window == null)
            return;
        _window.BackgroundsContainer.Children.Clear();

        if (!_prototype.TryIndex<BackgroundGroupPrototype>(BackgroundGroupID, out var backgroundGroup))
        {
            _sawmill.Error($"Не удалось найти группу предысторий с ID {BackgroundGroupID}");
            CloseWindow();
            return;
        }

        foreach (var backgroundProtoId in backgroundGroup.Backgrounds)
        {
            if (_prototype.TryIndex(backgroundProtoId, out var bgProto))
            {
                var backgroundControl = new BackgroundControl(bgProto.Name, bgProto.Description, bgProto.Skills, bgProto.Specials, bgProto.EasySkills, bgProto.SkillPoints);
                backgroundControl.OnPressed += () => _backGroundSystem.TakeGhostBackground(bgProto);
                _window.BackgroundsContainer.Children.Add(backgroundControl);
            }
            else
            {
                _sawmill.Error($"Не удалось найти предысторию с ID {backgroundProtoId}");                
            }
        }
        OpenWindow();
    }


    public void OnStateEntered(GameplayState state)
    {
        DebugTools.Assert(_window == null);
        _window = UIManager.CreateWindow<GhostBackgroundWindow>();
        LayoutContainer.SetAnchorPreset(_window, LayoutContainer.LayoutPreset.Wide);
    }

    public void OnStateExited(GameplayState state)
    {
        if (_window != null)
        {
            _window?.ForceClose();
            _window = null;
        }
    }
    public void OnSystemLoaded(BackgroundSystem system)
    {
        _player.LocalPlayerDetached += CharacterDetached;
    }

    public void OnSystemUnloaded(BackgroundSystem system)
    {
        _player.LocalPlayerDetached -= CharacterDetached;
    }

    public void CloseWindow()
    {
        if (_window != null)
        {
            _window.ForceClose();
        }
    }

    private void CharacterDetached(EntityUid uid)
    {
        CloseWindow();
    }

    private void OpenWindow()
    {
        if (_window != null && !_window.IsOpen)
        {
            _window.Open();
        }
    }

}
