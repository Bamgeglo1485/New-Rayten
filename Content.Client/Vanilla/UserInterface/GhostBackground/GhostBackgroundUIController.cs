using Content.Client.UserInterface.Systems.Gameplay;
using Content.Client.UserInterface.Controls;
using Content.Client.Vanilla.UserInterface.GhostBackground.window;
using Content.Client.Vanilla.UserInterface.Background;
using Content.Client.Vanilla.Background;
using Content.Client.Gameplay;
using Content.Shared.Vanilla.Background;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface;
using Robust.Client.Player;
using Robust.Shared.Utility;
using Robust.Shared.Prototypes;
using Robust.Shared.Player;
using JetBrains.Annotations;


namespace Content.Client.Vanilla.UserInterface.GhostBackground;

[UsedImplicitly]
public sealed class GhostBackgroundUIController : UIController, 
    IOnStateEntered<GameplayState>, 
    IOnStateExited<GameplayState>, 
    IOnSystemChanged<BackgroundSystem>
{
    private GhostBackgroundWindow? _window;
    private ProtoId<BackgroundGroupPrototype>? _pendingBackground;
    
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly ILogManager _logMan = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly GameplayStateLoadController _gameplayStateLoad = default!;
    [UISystemDependency] private readonly BackgroundSystem _backGroundSystem = default!;
    
    private ISawmill _sawmill = default!;

    public override void Initialize()
    {
        base.Initialize();
        _sawmill = _logMan.GetSawmill("ui.ghostbg");
        
        _gameplayStateLoad.OnScreenLoad += LoadGui;
        _gameplayStateLoad.OnScreenUnload += UnloadGui;
    }

    public void CreateBackground(ProtoId<BackgroundGroupPrototype> backgroundGroupId)
    {
        if (_window == null)
        {
            _pendingBackground = backgroundGroupId;
            return;
        }

        ApplyBackground(backgroundGroupId);
    }

    private void ApplyBackground(ProtoId<BackgroundGroupPrototype> backgroundGroupId)
    {
        _window!.BackgroundsContainer.Children.Clear();

        if (!_prototype.TryIndex(backgroundGroupId, out var backgroundGroup))
        {
            _sawmill.Error($"Не найдена группа предысторий: {backgroundGroupId}");
            CloseWindow();
            return;
        }

        foreach (var bgId in backgroundGroup.Backgrounds)
        {
            if (!_prototype.TryIndex(bgId, out var bgProto))
            {
                _sawmill.Error($"Не найдена предыстория: {bgId}");
                continue;
            }

            var control = new BackgroundControl(
                bgProto.Name,
                bgProto.Description,
                bgProto.SpecialDesc,
                bgProto.Skills,
                bgProto.EasySkills,
                bgProto.SkillPoints
            );
            
            control.OnPressed += () => _backGroundSystem.TakeGhostBackground(bgProto);
            _window.BackgroundsContainer.Children.Add(control);
        }

        OpenWindow();
    }

    private void LoadGui()
    {
        DebugTools.Assert(_window == null);
        _window = UIManager.CreateWindow<GhostBackgroundWindow>();
        LayoutContainer.SetAnchorPreset(_window, LayoutContainer.LayoutPreset.Wide);

        if (_pendingBackground != null)
        {
            ApplyBackground(_pendingBackground.Value);
            _pendingBackground = null;
        }
    }

    private void UnloadGui()
    {
        _window?.Dispose();
        _window = null;
    }

    public void OnSystemLoaded(BackgroundSystem system)
    {
        _player.LocalPlayerDetached += OnPlayerDetached;
    }

    public void OnSystemUnloaded(BackgroundSystem system)
    {
        _player.LocalPlayerDetached -= OnPlayerDetached;
    }

    private void OnPlayerDetached(EntityUid uid)
    {
        CloseWindow();
    }

    public void CloseWindow()
    {
        _window?.ForceClose();
    }

    private void OpenWindow()
    {
        if (_window is { IsOpen: false })
            _window.Open();
    }

    public void OnStateEntered(GameplayState state) { }
    public void OnStateExited(GameplayState state) => UnloadGui();
}