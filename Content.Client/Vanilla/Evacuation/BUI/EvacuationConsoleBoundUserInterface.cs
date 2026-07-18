using Content.Client.Computer;
using Content.Client.Vanilla.Evacuation.UI;
using Content.Shared.Vanilla.Evacuation.BUIStates;
using JetBrains.Annotations;

namespace Content.Client.Vanilla.Evacuation.BUI;

[UsedImplicitly]
public sealed class EvacuationConsoleBoundUserInterface : ComputerBoundUserInterface<EvacuationConsoleWindow, EvacuationConsoleBoundUserInterfaceState>
{
    public EvacuationConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }
}
