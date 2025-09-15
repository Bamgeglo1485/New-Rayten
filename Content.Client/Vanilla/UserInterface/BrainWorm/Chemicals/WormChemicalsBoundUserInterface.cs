using System.Linq;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Content.Shared.Vanilla.Entities.BrainWorm;

namespace Content.Client.Vanilla.UserInterface.BrainWorm.Chemicals;

public sealed class WormChemicalsBoundUserInterface : BoundUserInterface
{

    [ViewVariables]
    private WormChemicalsWindow? _menu;

    public WormChemicalsBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _menu = this.CreateWindowCenteredLeft<WormChemicalsWindow>();
        _menu.OnChemicalSelected += Select;
        PopulateChemicals();
    }

    private void PopulateChemicals()
    {
        if (EntMan.TryGetComponent<BrainWormComponent>(Owner, out var brainworm))
            _menu?.Populate(brainworm.Reagents, brainworm.Chemicals, brainworm.ChemicalsCup);
    }

    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        base.ReceiveMessage(message);

        if (_menu is null || message is not ChemicalsupdateMessage chemicalMessage)
            return;

        _menu.UpdateChemicalCount(chemicalMessage.Chemicals, chemicalMessage.ChemicalsCup);
    }

    public void Select(string chemical)
    {
        SendMessage(new ChemicalSelectMessage(chemical));
    }
}
