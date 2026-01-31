using Robust.Shared.Serialization;
using Robust.Shared.GameStates;
using Robust.Shared.Audio;
namespace Content.Shared.Vanilla.Competitive;

[RegisterComponent, NetworkedComponent]
public sealed partial class TechnicalAnalyzerComponent : Component
{
    [DataField]
    public ContrabandAnalysisData? CurrentAnalysisData = null;
    [DataField]
    public SoundSpecifier WinSound = new SoundPathSpecifier("/Audio/Machines/scan_finish.ogg");
    [DataField]
    public SoundSpecifier LoseSound = new SoundPathSpecifier("/Audio/Effects/Cargo/buzz_two.ogg");
}


[Serializable, NetSerializable]
public enum TechnicalAnalyzerUiKey : byte
{
    Key
}


[Serializable, NetSerializable]
public sealed class TechnicalAnalyzerInterfaceState : BoundUserInterfaceState
{
    public List<List<CodonFeedBack>> History { get; }
    public int AttemptsCount { get; }

    public TechnicalAnalyzerInterfaceState(
        List<List<CodonFeedBack>> history,
        int attemptsCount)
    {
        History = history;
        AttemptsCount = attemptsCount;
    }
}



[Serializable, NetSerializable]
public sealed class TechnicalAnalyzerButtonPressedMessage : BoundUserInterfaceMessage
{
    public List<char> SubmittedGenome { get; }

    public TechnicalAnalyzerButtonPressedMessage(List<char> submittedGenome)
    {
        SubmittedGenome = submittedGenome;
    }
}