using Content.Server.Discord;
using Content.Shared.Chat;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Content.Server.Chat.Systems;
using Robust.Shared.Timing;
using System.Text.RegularExpressions;

public sealed class DiscordChatRelaySystem : EntitySystem
{
    [Dependency] private readonly DiscordWebhook _discordWebhook = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private TimeSpan? NextTime;
    private  WebhookPayload? payload;
    private string _webhookUrl = "https://discord.com/api/webhooks/1357298293339717633/5_Z4WPq2DapHxGUR7aJC2Lq2QBR8JZtoDj0jI--_sQ1XGlqXT6TZdy_NxryaZpo_1vKC";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<EntitySpokeEvent>(OnEntitySpoke);
    }
    private (string Id, string Token) ParseWebhookUrl(string url)
    {
        var uri = new Uri(url);
        var segments = uri.Segments; // ["/", "api/", "webhooks/", "1234567890/", "ABCdefGHIjkLMnoPQRstUV"]
        
        if (segments.Length < 4)
            throw new ArgumentException("Некорректный URL вебхука Discord!");

        string id = segments[3].Trim('/');    // 1234567890
        string token = segments[4].Trim('/'); // ABCdefGHIjkLMnoPQRstUV
        return (id, token);
    }

    private async void OnEntitySpoke(EntitySpokeEvent ev)
    {
        if (_random.Prob(0.9f))
            return;
        
        if (!HasComp<ActorComponent>(ev.Source))
            return;

        if (NextTime == null || _timing.CurTime >= NextTime)
        {
            if (payload != null)
            {
                try
                {
                    var (id, token) = ParseWebhookUrl(_webhookUrl);
                    await _discordWebhook.CreateMessage(new WebhookIdentifier(id, token), payload.Value);
                    NextTime =  _timing.CurTime + TimeSpan.FromSeconds(_random.NextFloat(120, 300) );
                }
                catch (Exception ex)
                {
                    Log.Error($"Ошибка при отправке сообщения в Discord: {ex.Message}");
                }
            }

            string sanitizedMessage = Regex.Replace(ev.Message, @"[<>@#\*\\\|`]", "");

            if (string.IsNullOrEmpty(sanitizedMessage) || sanitizedMessage.Length > 200)
                return;

            payload = new WebhookPayload
            {
                Content = sanitizedMessage,
            };
        }
    }
}