using Content.Shared.Containers.ItemSlots;
using Content.Shared.Vanilla.Competitive;
using Content.Shared.Research.Components;
using Content.Server.Research.Systems;
using Robust.Server.GameObjects;
using Robust.Server.Audio;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using System.Linq;

namespace Content.Server.Vanilla.Competitive;

public sealed class TechnicalAnalysisSystem : EntitySystem
{
    private static readonly char[] Alphabet = ['A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'K'];
    public TimeSpan NextSpawn = TimeSpan.Zero;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly ResearchSystem _research = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly IGameTiming Timing = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TechnicalAnalyzerComponent, BoundUIOpenedEvent>(OnUIOpened);
        SubscribeLocalEvent<TechnicalAnalyzerComponent, ResearchRegistrationChangedEvent>(OnRegistrationChanged);
        SubscribeLocalEvent<TechnicalAnalyzerComponent, TechnicalAnalyzerButtonPressedMessage>(OnAnalyze);
    }
    public override void Update(float frameTime)
    {
        if (Timing.CurTime < NextSpawn)
            return;

        NextSpawn += TimeSpan.FromMinutes(5);

        var query = EntityQueryEnumerator<ContrabandBufferComponent>();
        while (query.MoveNext(out var uid, out var buffer))
        {
            var shuffled = Alphabet.ToList();
            _random.Shuffle(shuffled);
            var genome = shuffled.Take(6).ToList();

            var analysisData = new ContrabandAnalysisData
            {
                Genome = genome,
                History = new List<List<CodonFeedBack>>(),
                AttemptsCount = 5,
            };
            buffer.AnalyzedItems.Add(analysisData);
        }
    }

    private void OnRegistrationChanged(Entity<TechnicalAnalyzerComponent> console, ref ResearchRegistrationChangedEvent args)
    {
        console.Comp.CurrentAnalysisData = TryComp<ContrabandBufferComponent>(args.Server, out var buffer) && buffer.AnalyzedItems.Count > 0
                                            ? buffer.AnalyzedItems[0]
                                            : null;

        UpdateUI(console);
    }

    private void OnUIOpened(Entity<TechnicalAnalyzerComponent> console, ref BoundUIOpenedEvent args)
    {
        if (console.Comp.CurrentAnalysisData != null)
            return;

        if (TryComp<ResearchClientComponent>(console, out var rccomp)
            && TryComp<ContrabandBufferComponent>(rccomp.Server, out var buffer)
            && buffer.AnalyzedItems.Count > 0)
        {
            console.Comp.CurrentAnalysisData = buffer.AnalyzedItems[0];
        }

        UpdateUI(console);
    }

    private void SetDeferUI(Entity<TechnicalAnalyzerComponent> ent)
    {
        List<List<CodonFeedBack>> emptyHistory = [];
        _ui.SetUiState(ent.Owner, TechnicalAnalyzerUiKey.Key, new TechnicalAnalyzerInterfaceState(emptyHistory, -1));
    }

    private void UpdateUI(Entity<TechnicalAnalyzerComponent> ent)
    {
        var analysis = ent.Comp.CurrentAnalysisData;
        if (analysis == null)
        {
            SetDeferUI(ent);
            return;
        }

        var history = analysis.History;
        var attemptsCount = analysis.AttemptsCount;

        _ui.SetUiState(ent.Owner, TechnicalAnalyzerUiKey.Key, new TechnicalAnalyzerInterfaceState(history, attemptsCount));
    }

    private void OnAnalyze(Entity<TechnicalAnalyzerComponent> ent, ref TechnicalAnalyzerButtonPressedMessage args)
    {
        var analyzerComp = ent.Comp;

        if (!_research.TryGetClientServer(ent, out var server, out var serverComponent)
            || !TryComp<ContrabandBufferComponent>(server, out var buffer)
            || analyzerComp.CurrentAnalysisData == null)
        {
            SetDeferUI(ent);
            return;
        }

        if (analyzerComp.CurrentAnalysisData.AttemptsCount <= 0)
        {
            buffer.AnalyzedItems.Remove(analyzerComp.CurrentAnalysisData);
            analyzerComp.CurrentAnalysisData = null;
            UpdateUI(ent);
            return;
        }

        var submitted = args.SubmittedGenome;
        var correct = analyzerComp.CurrentAnalysisData.Genome;

        if (submitted.Count != correct.Count || submitted.Distinct().Count() != submitted.Count || !submitted.All(c => Alphabet.Contains(c)))
        {
            UpdateUI(ent);
            return;
        }

        var feedback = new List<CodonFeedBack>();
        var used = new bool[correct.Count];

        // Первый проход — точные совпадения
        for (int i = 0; i < submitted.Count; i++)
        {
            if (submitted[i] == correct[i])
            {
                feedback.Add(new CodonFeedBack
                {
                    Codon = submitted[i],
                    Hint = CodonHint.Correct
                });
                used[i] = true;
            }
            else
            {
                feedback.Add(null!); // будет заполнено позже
            }
        }

        // Второй проход — неправильная позиция
        for (int i = 0; i < submitted.Count; i++)
        {
            if (feedback[i] != null)
                continue;

            var codon = submitted[i];
            var found = false;

            for (int j = 0; j < correct.Count; j++)
            {
                if (!used[j] && correct[j] == codon)
                {
                    used[j] = true;
                    found = true;
                    break;
                }
            }

            feedback[i] = new CodonFeedBack
            {
                Codon = codon,
                Hint = found ? CodonHint.WrongPosition : CodonHint.Incorrect
            };
        }

        analyzerComp.CurrentAnalysisData.History.Add(feedback);
        bool win = feedback.All(f => f.Hint == CodonHint.Correct);

        if (win)
        {
            _research.ModifyServerPoints(server.Value, ContrabandAnalysisData.Award, serverComponent);
            buffer.AnalyzedItems.Remove(analyzerComp.CurrentAnalysisData);
            analyzerComp.CurrentAnalysisData = null;
            _audio.PlayPvs(analyzerComp.WinSound, ent);
        }
        else
        {
            analyzerComp.CurrentAnalysisData.AttemptsCount--;
            if (analyzerComp.CurrentAnalysisData.AttemptsCount <= 0)
            {
                _audio.PlayPvs(analyzerComp.LoseSound, ent);
                analyzerComp.CurrentAnalysisData = null;
            }
        }

        if (analyzerComp.CurrentAnalysisData == null)
        {
            if (buffer.AnalyzedItems.Count > 0)
                analyzerComp.CurrentAnalysisData = buffer.AnalyzedItems[0];
        }
        UpdateUI(ent);
    }
}
