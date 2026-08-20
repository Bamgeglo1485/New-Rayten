using Content.Shared.Vanilla.Moonsharp;
using Content.Server.Chat.Systems;
using Content.Shared.Chat;

namespace Content.Server.Vanilla.Moonsharp;

public sealed partial class ProgrammableDroneSystem : EntitySystem
{
    [Dependency] private ChatSystem _chat = default!;

    private readonly Dictionary<EntityUid, MoonsharpVM> _vms = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<ProgrammableDroneComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<ProgrammableDroneComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnStartup(Entity<ProgrammableDroneComponent> ent, ref ComponentStartup args)
    {
        var vm = new MoonsharpVM();
        _vms.Add(ent.Owner, vm);
    }

    private void OnShutdown(Entity<ProgrammableDroneComponent> ent, ref ComponentShutdown args)
    {
        _vms.Remove(ent.Owner);
    }

    public void Run(Entity<ProgrammableDroneComponent> ent)
    {
        if (!_vms.TryGetValue(ent.Owner, out var vm))
            return;

        ent.Comp.IsRunning = true;
        vm.Run(ent.Comp.Code);
    }

    public void Stop(Entity<ProgrammableDroneComponent> ent)
    {
        if (!_vms.TryGetValue(ent.Owner, out var vm))
            return;

        ent.Comp.IsRunning = false;
        vm.Run(ent.Comp.Code);
    }

    private void Say(Entity<ProgrammableDroneComponent> ent, string message)
    {
        _chat.TrySendInGameICMessage(ent, message, InGameICChatType.Speak, true);
    }
}
