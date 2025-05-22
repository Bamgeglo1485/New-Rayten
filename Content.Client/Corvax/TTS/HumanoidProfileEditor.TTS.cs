using System.Linq;
using Content.Client.Corvax.TTS;
using Content.Client.Lobby;
using Content.Corvax.Interfaces.Shared;
using Content.Shared.Corvax.TTS;
using Content.Shared.Preferences;
using Content.Shared.Vanilla.VoiceSpeech;
using Content.Client.Vanilla.VoiceSpeech;
using Content.Shared.Vanilla.Sponsor;
using Content.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Random;
using Timer = Robust.Shared.Timing.Timer;
using Robust.Shared.Player;
using Robust.Shared.Audio;

namespace Content.Client.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    private SharedSponsorManager? _sponsorsMgr;
    private List<VoiceSpeechPrototype> _voiceList = new();
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
            .EnumeratePrototypes<VoiceSpeechPrototype>()
            .Where(o => o.RoundStart)
            .OrderBy(o => Loc.GetString(o.Name))
            .ToList();

        Pitch.OnValueChanged += args =>
        {
            if (!MathHelper.CloseTo(PitchInput.Value, args.Value))
                PitchInput.Value = args.Value;

            SetVoicePitch(args.Value);
        };

        PitchInput.OnValueChanged += args =>
        {
            if (!MathHelper.CloseTo(Pitch.Value, args.Value))
                Pitch.Value = args.Value;

            SetVoicePitch(args.Value);
        };

        VoiceButton.OnItemSelected += args =>
        {
            VoiceButton.SelectId(args.Id);
            SetVoice(_voiceList[args.Id].ID);
        };

        VoicePlayButton.OnPressed += _ => PlayPreviewTTS();
        _sponsorsMgr = IoCManager.Resolve<SharedSponsorManager>();
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

            if (voice.SponsorOnly && _sponsorsMgr != null && !_sponsorsMgr.GetClientPrototypes().Contains(voice.ID))
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
        Pitch.Value = Profile.VoicePitch;
        PitchInput.Value = Profile.VoicePitch;
        PitchInput.IsValid = value => value >= 0.5f && value <= 1.5f;
    }

    private void PlayPreviewTTS()
    {
        if (Profile is null)
            return;
        var rng = IoCManager.Resolve<IRobustRandom>();
        var entMan = IoCManager.Resolve<IEntityManager>();
        var _audio = entMan.System<SharedAudioSystem>();
        var _undsys = entMan.System<VoiceSpeechSystem>();
        var previewBeepText = rng.Pick(_sampleText);

        _previewBeepIndex = 0;

        var voice = Profile.Voice;

        if(!_prototypeManager.TryIndex<VoiceSpeechPrototype>(voice, out var protoVoice))
            return;

        var Sound = protoVoice.Voice;

        void BeepStep()
        {
            if (_previewBeepIndex >= previewBeepText.Length)
                return;

            var nextChar = previewBeepText[_previewBeepIndex];

            _audio.PlayGlobal(Sound, Filter.Local(), true, AudioParams.Default.WithPitchScale(Profile.VoicePitch).WithVolume(1f));
            _previewBeepIndex++;

            if (_previewBeepIndex < previewBeepText.Length && _previewBeepIndex <= 55)
            {
                Timer.Spawn(TimeSpan.FromSeconds(rng.NextFloat(0.05f, 0.2f)), BeepStep);
            }
        }
        Timer.Spawn(TimeSpan.FromSeconds(rng.NextFloat(0.05f, 0.2f)), BeepStep);
    }
}
