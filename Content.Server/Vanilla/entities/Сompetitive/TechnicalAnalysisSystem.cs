using Content.Shared.Containers.ItemSlots;
using Content.Shared.Vanilla.Competitive;
using Content.Shared.Research.Components;
using Content.Server.Research.Systems;
using Robust.Server.GameObjects;
using Robust.Server.Audio;
using Robust.Shared.Random;
using System.Linq;

namespace Content.Server.Vanilla.Competitive;

public sealed class TechnicalAnalysisSystem : EntitySystem
{
    private static readonly char[] Alphabet = ['A', 'B', 'C', 'D', 'E', 'F', 'G', 'H'];
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ItemSlotsSystem _slots = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly ResearchSystem _research = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TechnicalAnalyzerComponent, BoundUIOpenedEvent>(OnUIOpened);
        SubscribeLocalEvent<TechnicalAnalyzerComponent, TechnicalAnalyzerButtonPressedMessage>(OnAnalyze);
        SubscribeLocalEvent<TechnicalAnalyzerComponent, TechnicalAnalyzerFullResetMessage>(OnGenomeReset);
        SubscribeLocalEvent<TechnicalAnalyzerComponent, TechnicalAnalyzerExtractMessage>(OnExtract);
    }
    private void OnUIOpened(Entity<TechnicalAnalyzerComponent> console, ref BoundUIOpenedEvent args)
    {
        if (console.Comp.CurrentAnalysisData == null)
        {
            if (!TryComp<ResearchClientComponent>(console, out var rccomp))
                return;

            if (!TryComp<ContrabandBufferComponent>(rccomp.Server, out var buffer))
            {
                SetDeferUI(console);
                return;
            }

            if (buffer.AnalyzedItems.Count == 0)
            {
                SetDeferUI(console);
                return;
            }
            // Берем первый элемент списка
            console.Comp.CurrentAnalysisData = buffer.AnalyzedItems[0];
        }

        UpdateUI(console);
    }

    /// <summary>
    ///  Устанавливается в ситуациях, когда нет подключённого сервера
    /// </summary>
    private void SetDeferUI(EntityUid uid)
    {
        if (!TryComp<TechnicalAnalyzerComponent>(uid, out var analyzerComp))
            return;

        int researchPoints = analyzerComp.ResearchPoints;

        if (!TryComp<ResearchClientComponent>(uid, out var rccomp) || rccomp.Server == null)
        {
            researchPoints = -1;
        }

        List<List<CodonFeedBack>> emptyHistory = new();
        _ui.SetUiState(uid, TechnicalAnalyzerUiKey.Key,
            new TechnicalAnalyzerInterfaceState(emptyHistory, -1, "Отсутствует", CompetitiveDifficult.medium, researchPoints));
        return;
    }

    private void UpdateUI(EntityUid uid)
    {
        if (!TryComp<TechnicalAnalyzerComponent>(uid, out var analyzerComp))
            return;

        var analysis = analyzerComp.CurrentAnalysisData;
        if (analysis == null)
        {
            SetDeferUI(uid);
            return;
        }

        var history = analysis.History;
        var attemptsCount = analysis.AttemptsCount;
        var sourceName = analysis.SourceName;
        var difficult = analysis.Difficult;

        _ui.SetUiState(uid, TechnicalAnalyzerUiKey.Key,
            new TechnicalAnalyzerInterfaceState(history, attemptsCount, sourceName, difficult, analyzerComp.ResearchPoints));
    }


    private void OnExtract(Entity<TechnicalAnalyzerComponent> ent, ref TechnicalAnalyzerExtractMessage args)
    {
        if (!_research.TryGetClientServer(ent, out var server, out var serverComponent))
            return;

        if (ent.Comp.ResearchPoints <= 0)
            return;

        _research.ModifyServerPoints(server.Value, ent.Comp.ResearchPoints, serverComponent);
        _audio.PlayPvs(ent.Comp.ExtractSound, ent);
        ent.Comp.ResearchPoints = 0;
        UpdateUI(ent);
    }

    private void OnGenomeReset(Entity<TechnicalAnalyzerComponent> ent, ref TechnicalAnalyzerFullResetMessage args)
    {
        var analyzerComp = ent.Comp;

        if (analyzerComp.CurrentAnalysisData is not { } data)
            return;

        if (data.Difficult == CompetitiveDifficult.hard)
            return;

        var shuffled = Alphabet.ToList();
        _random.Shuffle(shuffled);

        int genomeCount = data.Difficult == CompetitiveDifficult.easy ? 4 : 6;
        var genome = shuffled.Take(genomeCount).ToList();
        data.Genome = genome;
        data.History = new();
        data.AttemptsCount = 5;

        UpdateUI(ent);
    }

    private void OnAnalyze(Entity<TechnicalAnalyzerComponent> ent, ref TechnicalAnalyzerButtonPressedMessage args)
    {
        var analyzerComp = ent.Comp;

        if (!TryComp<ResearchClientComponent>(ent, out var rccomp))
            return;

        if (!TryComp<ContrabandBufferComponent>(rccomp.Server, out var buffer))
        {
            SetDeferUI(ent);
            return;
        }


        if (analyzerComp.CurrentAnalysisData == null)
        {
            SetDeferUI(ent);
            return;
        }

        if (analyzerComp.CurrentAnalysisData.AttemptsCount <= 0)
        {
            if (analyzerComp.CurrentAnalysisData.Difficult == CompetitiveDifficult.hard)
            {
                buffer.AnalyzedItems.Remove(analyzerComp.CurrentAnalysisData);
                analyzerComp.CurrentAnalysisData = null;
            }
            UpdateUI(ent);
            return;
        }

        var submitted = args.SubmittedGenome;
        var correct = analyzerComp.CurrentAnalysisData.Genome;

        if (submitted.Count != correct.Count)
        {
            UpdateUI(ent);
            return;
        }

        if (!submitted.All(c => Alphabet.Contains(c)))
        {
            UpdateUI(ent);
            return;
        }

        if (submitted.Distinct().Count() != submitted.Count)
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
            _audio.PlayPvs(analyzerComp.WinSound, ent);
            analyzerComp.ResearchPoints += analyzerComp.CurrentAnalysisData.CalculateResearchPointsAward();
            buffer.AnalyzedItems.Remove(analyzerComp.CurrentAnalysisData);
            analyzerComp.CurrentAnalysisData = null;
        }
        else
        {
            analyzerComp.CurrentAnalysisData.AttemptsCount--;
        }

        if (analyzerComp.CurrentAnalysisData == null)
        {
            if (buffer.AnalyzedItems.Count != 0)
                analyzerComp.CurrentAnalysisData = buffer.AnalyzedItems[0];
        }

        UpdateUI(ent);
    }

}
