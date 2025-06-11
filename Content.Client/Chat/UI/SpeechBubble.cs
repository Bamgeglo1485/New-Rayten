using System.Numerics;
using Content.Client.Chat.Managers;
using Content.Shared.CCVar;
using Content.Shared.Chat;
using Content.Shared.Speech;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using Content.Shared.Vanilla.VoiceSpeech;
using Content.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Audio;
using Content.Client.Vanilla.VoiceSpeech;
using Robust.Shared.Prototypes;
using System.Text.RegularExpressions;
namespace Content.Client.Chat.UI
{
    public abstract class SpeechBubble : Control
    {
        [Dependency] private readonly IGameTiming _timing = default!;
        [Dependency] private readonly IEyeManager _eyeManager = default!;
        [Dependency] private readonly IEntityManager _entityManager = default!;
        [Dependency] protected readonly IConfigurationManager ConfigManager = default!;
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
        protected RichTextLabel? _textLabel;
        private string _fullText = "";
        private int _revealedLength;
        private const float LetterDelay = 0.05f;
        private float _accumulatedTime;
        private Color? _fontColor;
        private bool _wasBold = false;
        protected virtual void InitializeText(ChatMessage message, Color? fontColor = null)
        {
            _fullText = SharedChatSystem.GetStringInsideTag(message, "BubbleContent");
            if (_fullText.Contains("[bold]") && _fullText.Contains("[/bold]"))
            {
                _wasBold = true;
            }
            _fullText = _fullText.Replace("[bold]", "").Replace("[/bold]", "");
            _fullText = Regex.Replace(_fullText, @"\[color=#(?:[0-9a-fA-F]{6}|[0-9a-fA-F]{8})\]", "");
            _fullText = _fullText.Replace("[/color]", "");

            _fontColor = fontColor;
            _revealedLength = 0;
            _accumulatedTime = 0;

            if (_textLabel != null)
            {
                _textLabel.SetMessage(FormatSpeech(_fullText, Color.FromHex("#00000000")));
            }
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
            if ( type == SpeechType.Say || type == SpeechType.Whisper )
            {
                var protoMan = IoCManager.Resolve<IPrototypeManager>();
                var entMan = IoCManager.Resolve<IEntityManager>();
                var speechsys = entMan.System<VoiceSpeechSystem>();

                if (!entMan.TryGetComponent<VoiceEmitterComponent>(senderEntity, out var undemitcomp)
                    || undemitcomp.VoicePrototypeId == null
                    || !protoMan.TryIndex<VoiceSpeechPrototype>(undemitcomp.VoicePrototypeId, out var protoVoice))
                    return bubble;

                undemitcomp.Voice = protoVoice.Voice;
                undemitcomp.Voice.Params = speechsys.SetVolume(type == SpeechType.Whisper, undemitcomp);
            }
            //rayten-end
            return bubble;
        }

        public SpeechBubble(ChatMessage message, EntityUid senderEntity, string speechStyleClass, Color? fontColor = null)
        {
            IoCManager.InjectDependencies(this);
            _senderEntity = senderEntity;
            _transformSystem = _entityManager.System<SharedTransformSystem>();

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
            if (_entityManager.TryGetComponent<VoiceEmitterComponent>(_senderEntity, out var comp) && comp.VoicePrototypeId != null)
            {
                var entMan = IoCManager.Resolve<IEntityManager>();
                var speechsys = entMan.System<VoiceSpeechSystem>();

                if (_textLabel != null && _revealedLength < _fullText.Length)
                {
                    _accumulatedTime += args.DeltaSeconds;

                    if (_accumulatedTime >= LetterDelay * (_fullText.Length - _revealedLength))
                    {
                        _revealedLength = _fullText.Length;
                        speechsys.Beep(_senderEntity, comp);
                    }
                    else if (_accumulatedTime >= LetterDelay)
                    {
                        _accumulatedTime -= LetterDelay;

                        var newChar = _fullText[_revealedLength];
                        if (newChar != ' ' && newChar != ',' && newChar != '.')
                            speechsys.Beep(_senderEntity, comp);

                        _revealedLength++;
                        timeLeft += LetterDelay;

                        if (_revealedLength >= 55)
                        {
                            _revealedLength = _fullText.Length;
                        }
                    }

                    var visible = _fullText.Substring(0, _revealedLength);
                    var hidden = _fullText.Substring(_revealedLength);
                    if (_wasBold)
                    {
                        visible = $"[bold]{visible}[/bold]";
                    }

                    var formatted = FormatSpeech(visible, _fontColor);
                    formatted.AddMarkupOrThrow($"[color=#00000000]{hidden}[/color]");
                    _textLabel.SetMessage(formatted);
                }
            }
            // RAYTEN-END
            if (timeLeft <= FadeTime.TotalSeconds)
            {
                // Update alpha if we're fading.
                Modulate = Color.White.WithAlpha(timeLeft / (float)FadeTime.TotalSeconds);
            }
            else
            {
                // Make opaque otherwise, because it might have been hidden before
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
            if (!ConfigManager.GetCVar(CCVars.ChatEnableFancyBubbles))
            {
                var label = new RichTextLabel
                {
                    MaxWidth = SpeechMaxWidth
                };

                label.SetMessage(ExtractAndFormatSpeechSubstring(message, "BubbleContent", fontColor));

                var unfanciedPanel = new PanelContainer
                {
                    StyleClasses = { "speechBox", speechStyleClass },
                    Children = { label },
                    ModulateSelfOverride = Color.White.WithAlpha(ConfigManager.GetCVar(CCVars.SpeechBubbleBackgroundOpacity)),
                };
                return unfanciedPanel;
            }

            var bubbleHeader = new RichTextLabel
            {
                ModulateSelfOverride = Color.White.WithAlpha(ConfigManager.GetCVar(CCVars.SpeechBubbleSpeakerOpacity)),
                Margin = new Thickness(1, 1, 1, 1),
            };

            _textLabel = new RichTextLabel
            {
                ModulateSelfOverride = Color.White.WithAlpha(ConfigManager.GetCVar(CCVars.SpeechBubbleTextOpacity)),
                MaxWidth = SpeechMaxWidth,
                Margin = new Thickness(2, 6, 2, 2),
                StyleClasses = { "bubbleContent" },
            };

            //We'll be honest. *Yes* this is hacky. Doing this in a cleaner way would require a bottom-up refactor of how saycode handles sending chat messages. -Myr
            bubbleHeader.SetMessage(ExtractAndFormatSpeechSubstring(message, "BubbleHeader", fontColor));

            var entMan = IoCManager.Resolve<IEntityManager>();

            if(senderEntity != null && entMan.TryGetComponent<VoiceEmitterComponent>(senderEntity, out var comp) && comp.VoicePrototypeId != null)
                InitializeText(message, fontColor);
            else
                _textLabel.SetMessage(ExtractAndFormatSpeechSubstring(message, "BubbleContent", fontColor));

            //As for below: Some day this could probably be converted to xaml. But that is not today. -Myr
            var mainPanel = new PanelContainer
            {
                StyleClasses = { "speechBox", speechStyleClass },
                Children = { _textLabel },
                ModulateSelfOverride = Color.White.WithAlpha(ConfigManager.GetCVar(CCVars.SpeechBubbleBackgroundOpacity)),
                HorizontalAlignment = HAlignment.Center,
                VerticalAlignment = VAlignment.Bottom,
                Margin = new Thickness(4, 14, 4, 2)
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
