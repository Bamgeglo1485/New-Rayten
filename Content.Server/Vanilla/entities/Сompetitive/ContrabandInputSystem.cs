using Content.Server.Chat.Systems;
using Content.Server.Forensics;
using Content.Server.Radio.EntitySystems;
using Content.Server.Station.Systems;
using Content.Server.Audio;
using Content.Shared.Storage;
using Content.Shared.Paper;
using Content.Shared.Vanilla.Competitive;
using Content.Shared.Throwing;
using Content.Shared.Research.Components;
using Content.Shared.Containers.ItemSlots;
using Robust.Server.Containers;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Random;
using Robust.Shared.Maths;
using System.Numerics;
using System.Linq;
using System.Text;

namespace Content.Server.Vanilla.Competitive;

public sealed class ContrabandInputSystem : SharedContrabandInputSystem
{
    private static readonly char[] Alphabet = ['A', 'B', 'C', 'D', 'E', 'F', 'G', 'H'];
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly AmbientSoundSystem _ambientSound = default!;
    [Dependency] private readonly ThrowingSystem _throwingSystem = default!;
    [Dependency] private readonly ContainerSystem _container = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly RadioSystem _radio = default!;
    [Dependency] private readonly PaperSystem _paperSystem = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly StationSystem _station = default!;
    public override void Initialize()
    {
        SubscribeLocalEvent<ContrabandInputComponent, EntInsertedIntoContainerMessage>(OnInserted);
    }

    private void OnInserted(EntityUid uid, ContrabandInputComponent component, EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != component.SlotId)
            return;

        component.Analysing = true;
        Dirty(uid, component);
        var insertedEntity = args.Entity;
        _appearance.SetData(uid, ContrabandAnalyzerVisuals.Accept, true);
        _ambientSound.SetAmbience(uid, true);
        _chat.TrySendInGameICMessage(uid, Loc.GetString("contraband-analyzer-analysis"),
            InGameICChatType.Speak, true);

        uid.SpawnTimer(TimeSpan.FromSeconds(component.ScanTime), () =>
        {
            _appearance.SetData(uid, ContrabandAnalyzerVisuals.Accept, false);
            _ambientSound.SetAmbience(uid, false);
            component.Analysing = false;
            Dirty(uid, component);
            if (!TryComp<ResearchClientComponent>(uid, out var servercomp) || !servercomp.ConnectedToServer)
            {
                EjectItem(uid, insertedEntity, component.SlotId, component, "contraband-analyzer-analysis-noserver");
                return;
            }

            if (TryComp<CompetitiveComponent>(insertedEntity, out var competcomp))
            {
                if (!TryComp<ContrabandBufferComponent>(servercomp.Server, out var contrabuffer))
                {
                    EjectItem(uid, insertedEntity, component.SlotId, component, "contraband-analyzer-analysis-noserver");
                    return;
                }

                Analyze(uid, insertedEntity, competcomp, contrabuffer);
            }
            else
            {
                EjectItem(uid, insertedEntity, component.SlotId, component, "contraband-analyzer-analysis-NotContraband");
            }
        });
    }

    private void Analyze(EntityUid machine, EntityUid ent, CompetitiveComponent comp, ContrabandBufferComponent buffer)
    {


        _audio.PlayPvs("/Audio/Machines/scan_finish.ogg", machine);
        _chat.TrySendInGameICMessage(machine, Loc.GetString("contraband-analyzer-analysis-accept"),
            InGameICChatType.Speak, true);

        //печатаем документ
        SpawnPaper(machine, ent, comp);

        //оповещаем рнд
        _radio.SendRadioMessage(machine, Loc.GetString("contraband-analyzer-radio-message"), "Science", machine);

        //отправляем данные на сервер РНД
        var shuffled = Alphabet.ToList();
        _random.Shuffle(shuffled);
        int genomeCount = comp.Difficult == CompetitiveDifficult.easy ? 4 : 6;
        var genome = shuffled.Take(genomeCount).ToList();

        var analysisData = new ContrabandAnalysisData
        {
            Genome = genome,
            History = new List<List<CodonFeedBack>>(),
            Difficult = comp.Difficult,
            SourceDesc = comp.SourceDesc,
            AttemptsCount = 5,
            SourceName = Name(ent),
        };
        buffer.AnalyzedItems.Add(analysisData);

        QueueDel(ent);
    }

    private void EjectItem(EntityUid uid, EntityUid itemEntity, string slotId, ContrabandInputComponent component, LocId? reason = null)
    {
        if (!TryComp<ContainerManagerComponent>(uid, out var containerManager))
            return;

        if (!containerManager.TryGetContainer(component.SlotId, out var container))
            return;

        _container.Remove(itemEntity, container);

        // Генерируем случайное направление
        var throwDir = new Vector2(_random.NextFloat(-1f, 1f), _random.NextFloat(-1f, 1f));

        // Нормализуем, чтобы получить чистое направление (без усиления из-за длины)
        if (throwDir != Vector2.Zero)
            throwDir = throwDir.Normalized();

        // Бросаем предмет
        _throwingSystem.TryThrow(itemEntity, throwDir, 20f);

        _audio.PlayPvs("/Audio/Machines/Nuke/angry_beep.ogg", uid);
        _chat.TrySendInGameICMessage(uid, Loc.GetString(reason ?? "contraband-analyzer-analysis-error"),
            InGameICChatType.Speak, true);
    }
    private void SpawnPaper(EntityUid machine, EntityUid contraband, CompetitiveComponent comp)
    {
        var printed = EntityManager.SpawnEntity("Paper", Transform(machine).Coordinates);

        if (TryComp<PaperComponent>(printed, out var paper))
        {
            var stationuid = _station.GetOwningStation(machine);
            string stationname = stationuid.HasValue
                ? MetaData(stationuid.Value).EntityName
                : "неизвестна";

            var fingerprints = new StringBuilder();
            var fibers = new StringBuilder();
            var touchDNAs = new StringBuilder();
            var residues = new StringBuilder();

            if (TryComp<ForensicsComponent>(contraband, out var forensics))
            {
                if (forensics.Fingerprints.Count > 0)
                {
                    foreach (var fingerprint in forensics.Fingerprints)
                        fingerprints.AppendLine(fingerprint);
                }
                else
                    fingerprints.AppendLine("отсутствуют");

                if (forensics.Fibers.Count > 0)
                {
                    foreach (var fiber in forensics.Fibers)
                        fibers.AppendLine(fiber);
                }
                else
                    fibers.AppendLine("отсутствуют");

                if (forensics.DNAs.Count > 0)
                {
                    foreach (var dna in forensics.DNAs)
                        touchDNAs.AppendLine(dna);
                }
                else
                    touchDNAs.AppendLine("отсутствуют");

                if (forensics.Residues.Count > 0)
                {
                    foreach (var residue in forensics.Residues)
                        residues.AppendLine(residue);
                }
                else
                    residues.AppendLine("отсутствуют");
            }
            else
            {
                fingerprints.AppendLine("отсутствуют");
                fibers.AppendLine("отсутствуют");
                touchDNAs.AppendLine("отсутствуют");
                residues.AppendLine("отсутствуют");
            }

            // Заполняем бумагу
            _paperSystem.SetContent((printed, paper),
                Loc.GetString("contraband-analyzer-paper-content",
                    ("station", stationname),
                    ("actualname", comp.ActualName),
                    ("hiddendesc", Loc.GetString(comp.HiddenDesc)),
                    ("enemyTechnology", comp.EnemyTechnology),
                    ("fingerprints", fingerprints.ToString().TrimEnd()),
                    ("fibers", fibers.ToString().TrimEnd()),
                    ("touchDNAs", touchDNAs.ToString().TrimEnd()),
                    ("residues", residues.ToString().TrimEnd())
                ));

            StampDisplayInfo stamp = new() { StampedName = Loc.GetString("stamp-component-stamped-name-contraband-analyzer"), StampedColor = Color.FromHex("#ff4242") };
            _paperSystem.TryStamp((printed, paper), stamp, "paper_stamp-warden");
            paper.EditingDisabled = true;

            _metaData.SetEntityName(printed, $"Анализ {MetaData(contraband).EntityName}");
        }
    }
}
