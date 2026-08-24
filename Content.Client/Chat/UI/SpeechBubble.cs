using System.Numerics;
using Content.Client.Vanilla.VoiceSpeech;
using Content.Shared.CCVar;
using Content.Shared.Chat;
using Content.Shared.Speech;
using Content.Shared.Vanilla.VoiceSpeech;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using Robust.Shared.Prototypes;
using System.Text.RegularExpressions;
using System.Text;

namespace Content.Client.Chat.UI
{
    public abstract partial class SpeechBubble : Control
    {
        [Dependency] private IGameTiming _timing = default!;
        [Dependency] private IEyeManager _eyeManager = default!;
        [Dependency] private IEntityManager _entityManager = default!;
        [Dependency] protected IConfigurationManager ConfigManager = default!;
        private readonly SharedTransformSystem _transformSystem;

        public enum SpeechType : byte
        {
            Emote,
            Say,
            Whisper,
            Looc
        }

        /// <summary>
        ///     The total time a speech bubble stays on screen.
        /// </summary>
        private static readonly TimeSpan TotalTime = TimeSpan.FromSeconds(4);

        /// <summary>
        ///     The amount of time at the end of the bubble's life at which it starts fading.
        /// </summary>
        private static readonly TimeSpan FadeTime = TimeSpan.FromSeconds(0.25f);

        /// <summary>
        ///     The distance in world space to offset the speech bubble from the center of the entity.
        ///     i.e. greater -> higher above the mob's head.
        /// </summary>
        private const float EntityVerticalOffset = 0.5f;

        /// <summary>
        ///     The default maximum width for speech bubbles.
        /// </summary>
        public const float SpeechMaxWidth = 256;

        private readonly EntityUid _senderEntity;

        /// <summary>
        /// The time at which this bubble will die.
        /// </summary>
        private TimeSpan _deathTime;

        public float VerticalOffset { get; set; }
        private float _verticalOffsetAchieved;

        public Vector2 ContentSize { get; private set; }
        //Rayten-start
        protected RichTextLabel? TextLabel;
        private string _fullText = "";
        private int _revealedLength;
        private const float LetterDelay = 0.045f;
        private const float PunctuationDelay = 0.2f;
        private float _fadeElapsed = 0;
        private float _accumulatedTime;
        private Color? _fontColor;
        private bool _wasBold = false;
        private IEntityManager _entMan = default!;
        private VoiceSpeechSystem _speechSys = default!;
        private static readonly Regex BbTagRegex =
            new(@"\[/?[a-zA-Z0-9#=]+\]", RegexOptions.Compiled);
        protected void InitializeText(ChatMessage message, Color? fontColor = null)
        {
            _fullText = SharedChatSystem.GetStringInsideTag(message, "BubbleContent");

            // Проверяем bold ДО очистки
            _wasBold = _fullText.Contains("[bold]", StringComparison.Ordinal);

            // Удаляем ВСЕ BB-теги одним проходом
            _fullText = BbTagRegex.Replace(_fullText, string.Empty);

            _fontColor = fontColor;
            _revealedLength = 0;
            _accumulatedTime = 0;

            TextLabel?.SetMessage(
                FormatSpeech(_fullText, Color.FromHex("#00000000"))
            );
        }
        //Rayten-end
        // man down
        public event Action<EntityUid, SpeechBubble>? OnDied;

        public static SpeechBubble CreateSpeechBubble(SpeechType type, ChatMessage message, EntityUid senderEntity)
        {
            SpeechBubble bubble = type switch
            {
                SpeechType.Emote => new TextSpeechBubble(message, senderEntity, "emoteBox"),
                SpeechType.Say => new FancyTextSpeechBubble(message, senderEntity, "sayBox"),
                SpeechType.Whisper => new FancyTextSpeechBubble(message, senderEntity, "whisperBox"),
                SpeechType.Looc => new TextSpeechBubble(message, senderEntity, "emoteBox", Color.FromHex("#48d1cc")),
                _ => throw new ArgumentOutOfRangeException()
            };
            //rayten-start
            if (type == SpeechType.Say || type == SpeechType.Whisper)
            {
                var protoMan = IoCManager.Resolve<IPrototypeManager>();
                var entMan = IoCManager.Resolve<IEntityManager>();
                var speechsys = entMan.System<VoiceSpeechSystem>();

                if (!entMan.TryGetComponent<VoiceEmitterComponent>(senderEntity, out var voicecomp)
                    || voicecomp.VoicePrototypeId == null
                    || !protoMan.TryIndex<VoiceSpeechPrototype>(voicecomp.VoicePrototypeId, out var protoVoice))
                    return bubble;

                voicecomp.Voice = protoVoice.Voice;
                voicecomp.Voice.Params = speechsys.SetVolume(type == SpeechType.Whisper, voicecomp, protoVoice.Basevolume);
            }
            //rayten-end
            return bubble;
        }

        public SpeechBubble(ChatMessage message, EntityUid senderEntity, string speechStyleClass, Color? fontColor = null)
        {
            IoCManager.InjectDependencies(this);
            _senderEntity = senderEntity;
            _transformSystem = _entityManager.System<SharedTransformSystem>();
            _entMan = IoCManager.Resolve<IEntityManager>();
            _speechSys = _entMan.System<VoiceSpeechSystem>();
            // Use text clipping so new messages don't overlap old ones being pushed up.
            RectClipContent = true;

            var bubble = BuildBubble(message, speechStyleClass, fontColor, senderEntity);

            AddChild(bubble);

            ForceRunStyleUpdate();

            bubble.Measure(Vector2Helpers.Infinity);
            ContentSize = bubble.DesiredSize;
            _verticalOffsetAchieved = -ContentSize.Y;
            _deathTime = _timing.RealTime + TotalTime;
        }

        protected abstract Control BuildBubble(ChatMessage message, string speechStyleClass, Color? fontColor = null, EntityUid? senderEntity = null);

        protected override void FrameUpdate(FrameEventArgs args)
        {
            base.FrameUpdate(args);

            var timeLeft = (float)(_deathTime - _timing.RealTime).TotalSeconds;
            if (_entityManager.Deleted(_senderEntity) || timeLeft <= 0)
            {
                // Timer spawn to prevent concurrent modification exception.
                Timer.Spawn(0, Die);
                return;
            }

            // Lerp to our new vertical offset if it's been modified.
            if (MathHelper.CloseToPercent(_verticalOffsetAchieved - VerticalOffset, 0, 0.1))
            {
                _verticalOffsetAchieved = VerticalOffset;
            }
            else
            {
                _verticalOffsetAchieved = MathHelper.Lerp(_verticalOffsetAchieved, VerticalOffset, 10 * args.DeltaSeconds);
            }

            if (!_entityManager.TryGetComponent<TransformComponent>(_senderEntity, out var xform) || xform.MapID != _eyeManager.CurrentEye.Position.MapId)
            {
                // Modulate = Color.White.WithAlpha(0);
                // return;
                Timer.Spawn(0, Die);
                return;
            }
            // RAYTEN-START
            if (_entityManager.TryGetComponent<VoiceEmitterComponent>(_senderEntity, out var comp)
                && comp.VoicePrototypeId != null
                && TextLabel != null
                && _revealedLength < _fullText.Length)
            {
                _accumulatedTime += args.DeltaSeconds;
                var sb = new StringBuilder(_revealedLength + 16);
                // Показываем новую букву, если пришло время
                if (_accumulatedTime >= LetterDelay)
                {
                    _accumulatedTime -= LetterDelay;
                    _deathTime += TimeSpan.FromSeconds(LetterDelay);

                    var newChar = _fullText[_revealedLength];

                    // звук только на "обычные" символы
                    if (!" ,.!?".Contains(newChar))
                    {
                        _speechSys.Beep(_senderEntity, comp);
                    }
                    else if (!char.IsWhiteSpace(newChar))
                    {
                        // пунктуация — замедляем вывод
                        _accumulatedTime -= PunctuationDelay;
                        _deathTime += TimeSpan.FromSeconds(PunctuationDelay);
                    }
                    _revealedLength++;
                }

                // ---------- Формирование текста ----------
                // visible
                sb.Append(_fullText, 0, _revealedLength);

                if (_revealedLength < _fullText.Length)
                    sb.Append('…');

                if (_wasBold)
                {
                    sb.Insert(0, "[bold]");
                    sb.Append("[/bold]");
                }

                // hidden
                var hidden = _revealedLength < _fullText.Length
                    ? _fullText.Substring(_revealedLength)
                    : string.Empty;

                var formatted = FormatSpeech(sb.ToString(), _fontColor);
                formatted.AddMarkupOrThrow($"[color=#00000000]{hidden}[/color]");
                TextLabel.SetMessage(formatted);
            }

            // --- RAYTEN-END ---
            // Плавный фейд текста
            _fadeElapsed += args.DeltaSeconds;
            if (_fadeElapsed <= FadeTime.TotalSeconds)
            {
                var alpha = MathHelper.Clamp(_fadeElapsed / (float)FadeTime.TotalSeconds, 0f, 1f);
                Modulate = Color.White.WithAlpha(alpha);
            }
            else
            {
                Modulate = Color.White;
            }


            var baseOffset = 0f;

            if (_entityManager.TryGetComponent<SpeechComponent>(_senderEntity, out var speech))
                baseOffset = speech.SpeechBubbleOffset;

            var offset = (-_eyeManager.CurrentEye.Rotation).ToWorldVec() * -(EntityVerticalOffset + baseOffset);
            var worldPos = _transformSystem.GetWorldPosition(xform) + offset;

            var lowerCenter = _eyeManager.WorldToScreen(worldPos) / UIScale;
            var screenPos = lowerCenter - new Vector2(ContentSize.X / 2, ContentSize.Y + _verticalOffsetAchieved);
            // Round to nearest 0.5
            screenPos = (screenPos * 2).Rounded() / 2;
            LayoutContainer.SetPosition(this, screenPos);

            var height = MathF.Ceiling(MathHelper.Clamp(lowerCenter.Y - screenPos.Y, 0, ContentSize.Y));
            SetHeight = height;

        }

        private void Die()
        {
            if (Disposed)
            {
                return;
            }

            OnDied?.Invoke(_senderEntity, this);
        }

        /// <summary>
        ///     Causes the speech bubble to start fading IMMEDIATELY.
        /// </summary>
        public void FadeNow()
        {
            if (_deathTime > _timing.RealTime)
            {
                _deathTime = _timing.RealTime + FadeTime;
            }
        }

        protected FormattedMessage FormatSpeech(string message, Color? fontColor = null)
        {
            var msg = new FormattedMessage();
            if (fontColor != null)
                msg.PushColor(fontColor.Value);
            msg.AddMarkupOrThrow(message);
            return msg;
        }

        protected FormattedMessage ExtractAndFormatSpeechSubstring(ChatMessage message, string tag, Color? fontColor = null)
        {
            return FormatSpeech(SharedChatSystem.GetStringInsideTag(message, tag), fontColor);
        }


    }

    public sealed class TextSpeechBubble : SpeechBubble
    {
        public TextSpeechBubble(ChatMessage message, EntityUid senderEntity, string speechStyleClass, Color? fontColor = null)
            : base(message, senderEntity, speechStyleClass, fontColor)
        {
        }

        protected override Control BuildBubble(ChatMessage message, string speechStyleClass, Color? fontColor = null, EntityUid? senderEntity = null)
        {
            var label = new RichTextLabel
            {
                MaxWidth = SpeechMaxWidth,
                OutlineColorOverride = TextOutline.Default.Color,
            };

            label.SetMessage(FormatSpeech(message.WrappedMessage, fontColor));

            var panel = new PanelContainer
            {
                StyleClasses = { "speechBox", speechStyleClass },
                Children = { label },
                ModulateSelfOverride = Color.White.WithAlpha(ConfigManager.GetCVar(CCVars.SpeechBubbleBackgroundOpacity))
            };

            return panel;
        }
    }

    public sealed class FancyTextSpeechBubble : SpeechBubble
    {

        public FancyTextSpeechBubble(ChatMessage message, EntityUid senderEntity, string speechStyleClass, Color? fontColor = null)
            : base(message, senderEntity, speechStyleClass, fontColor)
        {
        }

        protected override Control BuildBubble(ChatMessage message, string speechStyleClass, Color? fontColor = null, EntityUid? senderEntity = null)
        {
            var entMan = IoCManager.Resolve<IEntityManager>();
            var withVoice = senderEntity != null && entMan.TryGetComponent<VoiceEmitterComponent>(senderEntity, out var comp) && comp.VoicePrototypeId != null;
            if (!ConfigManager.GetCVar(CCVars.ChatEnableFancyBubbles))
            {
                TextLabel = new RichTextLabel//rayten-global-var
                {
                    MaxWidth = SpeechMaxWidth,
                    OutlineColorOverride = TextOutline.Default.Color,
                };

                //rayten-start
                if (withVoice)
                    InitializeText(message, fontColor);
                else
                    TextLabel.SetMessage(ExtractAndFormatSpeechSubstring(message, "BubbleContent", fontColor));
                //rayten-end

                var unfanciedPanel = new PanelContainer
                {
                    StyleClasses = { "speechBox", speechStyleClass },
                    Children = { TextLabel },
                    ModulateSelfOverride = Color.White.WithAlpha(ConfigManager.GetCVar(CCVars.SpeechBubbleBackgroundOpacity)),
                };
                return unfanciedPanel;
            }

            var bubbleHeader = new RichTextLabel
            {
                ModulateSelfOverride = Color.White.WithAlpha(ConfigManager.GetCVar(CCVars.SpeechBubbleSpeakerOpacity)),
                Margin = new Thickness(2, 0, 2, 0),
                OutlineColorOverride = TextOutline.Default.Color,
            };

            TextLabel = new RichTextLabel
            {
                ModulateSelfOverride = Color.White.WithAlpha(ConfigManager.GetCVar(CCVars.SpeechBubbleTextOpacity)),
                MaxWidth = SpeechMaxWidth,
                Margin = new Thickness(2, 0, 2, 0),
                StyleClasses = { "bubbleContent" },
                OutlineColorOverride = TextOutline.Default.Color,
            };

            //We'll be honest. *Yes* this is hacky. Doing this in a cleaner way would require a bottom-up refactor of how saycode handles sending chat messages. -Myr
            bubbleHeader.SetMessage(ExtractAndFormatSpeechSubstring(message, "BubbleHeader", fontColor));

            //rayten-start
            if (withVoice)
                InitializeText(message, fontColor);
            else
                TextLabel.SetMessage(ExtractAndFormatSpeechSubstring(message, "BubbleContent", fontColor));
            //rayten-end

            //As for below: Some day this could probably be converted to xaml. But that is not today. -Myr
            var mainPanel = new PanelContainer
            {
                StyleClasses = { "speechBox", speechStyleClass },
                Children = { TextLabel },
                ModulateSelfOverride = Color.White.WithAlpha(ConfigManager.GetCVar(CCVars.SpeechBubbleBackgroundOpacity)),
                HorizontalAlignment = HAlignment.Center,
                VerticalAlignment = VAlignment.Bottom,
                Margin = new Thickness(4, 20, 4, 2)
            };

            var headerPanel = new PanelContainer
            {
                StyleClasses = { "speechBox", speechStyleClass },
                Children = { bubbleHeader },
                ModulateSelfOverride = Color.White.WithAlpha(ConfigManager.GetCVar(CCVars.ChatFancyNameBackground) ? ConfigManager.GetCVar(CCVars.SpeechBubbleBackgroundOpacity) : 0f),
                HorizontalAlignment = HAlignment.Center,
                VerticalAlignment = VAlignment.Top
            };

            var panel = new PanelContainer
            {
                Children = { mainPanel, headerPanel }
            };

            return panel;
        }
    }
}
