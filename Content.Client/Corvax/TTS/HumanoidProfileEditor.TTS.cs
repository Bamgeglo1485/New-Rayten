using System.Linq;
using Content.Client.Corvax.TTS;
using Content.Client.Lobby;
using Content.Corvax.Interfaces.Shared;
using Content.Shared.Corvax.TTS;
using Content.Shared.Preferences;
using Content.Shared.Vanilla.UndertaleSpeech;
using Content.Client.Vanilla.UndertaleSpeech;
using Content.Shared.Audio; 
using Robust.Shared.Audio.Systems;
using Robust.Shared.Random;
using Timer = Robust.Shared.Timing.Timer;
using Robust.Shared.Player;
using Robust.Shared.Audio;

namespace Content.Client.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    private ISharedSponsorsManager? _sponsorsMgr;
    private List<UndertaleSpeechPrototype> _voiceList = new();
    private readonly List<string> _sampleText =
        new()
        {
            "Съешь же ещё этих мягких французских булок, да выпей чаю.",
            "Клоун, прекрати разбрасывать банановые кожурки офицерам под ноги!",
            "Капитан, вы уверены что хотите назначить клоуна на должность главы персонала?",
            "Эс Бэ! Тут человек в сером костюме, с тулбоксом и в маске! Помогите!!",
            "Учёные, тут странная аномалия в баре! Она уже съела мима!",
            "Я надеюсь что инженеры внимательно следят за сингулярностью...",
            "Вы слышали эти странные крики в техах? Мне кажется туда ходить небезопасно.",
            "Вы не видели Гамлета? Мне кажется он забегал к вам на кухню.",
            "Здесь есть доктор? Человек умирает от отравленного пончика! Нужна помощь!",
            "Вам нужно согласие и печать квартирмейстера, если вы хотите сделать заказ на партию дробовиков.",
            "Возле эвакуационного шаттла разгерметизация! Инженеры, нам срочно нужна ваша помощь!",
            "Бармен, налей мне самого крепкого вина, которое есть в твоих запасах!"
        };
    private int _previewBeepIndex;
    private void InitializeVoice()
    {
        _voiceList = _prototypeManager
            .EnumeratePrototypes<UndertaleSpeechPrototype>()
            .Where(o => o.RoundStart)
            .OrderBy(o => Loc.GetString(o.Name))
            .ToList();

        VoiceButton.OnItemSelected += args =>
        {
            VoiceButton.SelectId(args.Id);
            SetVoice(_voiceList[args.Id].ID);
        };

        VoicePlayButton.OnPressed += _ => PlayPreviewTTS();

        IoCManager.Instance!.TryResolveType(out _sponsorsMgr);
    }

    private void UpdateTTSVoicesControls()
    {
        if (Profile is null)
            return;

        VoiceButton.Clear();

        var firstVoiceChoiceId = 1;
        for (var i = 0; i < _voiceList.Count; i++)
        {
            var voice = _voiceList[i];
            if (!HumanoidCharacterProfile.CanHaveVoice(voice, Profile.Sex))
                continue;

            var name = Loc.GetString(voice.Name);
            VoiceButton.AddItem(name, i);

            if (firstVoiceChoiceId == 1)
                firstVoiceChoiceId = i;

            if (_sponsorsMgr is null)
                continue;
            if (voice.SponsorOnly && _sponsorsMgr != null &&
                !_sponsorsMgr.GetClientPrototypes().Contains(voice.ID))
            {
                VoiceButton.SetItemDisabled(VoiceButton.GetIdx(i), true);
            }
        }

        var voiceChoiceId = _voiceList.FindIndex(x => x.ID == Profile.Voice);
        if (!VoiceButton.TrySelectId(voiceChoiceId) &&
            VoiceButton.TrySelectId(firstVoiceChoiceId))
        {
            SetVoice(_voiceList[firstVoiceChoiceId].ID);
        }
    }

    private void PlayPreviewTTS()
    {
        if (Profile is null)
            return;
        var rng = IoCManager.Resolve<IRobustRandom>(); 
        var entMan = IoCManager.Resolve<IEntityManager>();
        var _audio = entMan.System<SharedAudioSystem>();
        var _undsys = entMan.System<UndertaleSpeechSystem>();
        var previewBeepText = rng.Pick(_sampleText);

        _previewBeepIndex = 0;

        var voice = Profile.Voice;

        if(!_prototypeManager.TryIndex<UndertaleSpeechPrototype>(voice, out var protoVoice))
            return;

        var Sound = protoVoice.Voice;

        void BeepStep()
        {
            if (_previewBeepIndex >= previewBeepText.Length)
                return;

            var nextChar = previewBeepText[_previewBeepIndex];

            _audio.PlayGlobal(Sound, Filter.Local(), true, AudioParams.Default.WithVolume(_undsys.AdjustVolume(false)));
            _previewBeepIndex++;

            if (_previewBeepIndex < previewBeepText.Length && _previewBeepIndex <= 40)
            { 
                Timer.Spawn(TimeSpan.FromSeconds(rng.NextFloat(0.05f, 0.2f)), BeepStep);
            }
        }
        Timer.Spawn(TimeSpan.FromSeconds(rng.NextFloat(0.05f, 0.2f)), BeepStep);
    }
}
