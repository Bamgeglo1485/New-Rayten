using Content.Shared.Vanilla.Competitive;
using Content.Shared.Research.Components;
using Content.Client.Vanilla.TechAnalysis;
using Robust.Client.UserInterface;
using JetBrains.Annotations;

namespace Content.Client.Vanilla.Competitive;

[UsedImplicitly]
public sealed class TechnicalAnalysisBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private TechnicalAnalysisMenu? _consoleMenu;

    protected override void Open()
    {
        base.Open();

        _consoleMenu = this.CreateWindow<TechnicalAnalysisMenu>();

        _consoleMenu.OnClose += Close;
        _consoleMenu.OpenCentered();

        _consoleMenu.OnSubmitPressed += guess =>
        {
            SendMessage(new TechnicalAnalyzerButtonPressedMessage(guess));
        };

        _consoleMenu.OnFullResetPressed += () =>
        {
            SendMessage(new TechnicalAnalyzerFullResetMessage());
        };

        _consoleMenu.OnExtractPressed += () =>
        {
            SendMessage(new TechnicalAnalyzerExtractMessage());
        };

        _consoleMenu.OnServerSelectionButtonPressed += () =>
        {
            SendMessage(new ConsoleServerSelectionMessage());
        };
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not TechnicalAnalyzerInterfaceState msg)
            return;

        _consoleMenu?.Update(msg.History, msg.AttemptsCount, msg.SourceName, msg.Difficult);
        _consoleMenu?.ExtractButtonUpdate(msg.ResearchPoints);
        _consoleMenu?.NoItem(msg.AttemptsCount == -1);
    }


    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
            return;

        _consoleMenu?.Dispose();
    }
}
