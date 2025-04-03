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
    private string _webhookUrl = "https://discord.com/api/webhooks/1272975564566691951/ftKDmGcNDm2wBRq12506ql6gCQ8KX4naTT2C58kdp19HSwPghZhbd3yjfy64GqcpLqkr";

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
        if (_random.Prob(0.95f))
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
                    NextTime =  _timing.CurTime + TimeSpan.FromSeconds(_random.NextFloat(240, 480) );
                }
                catch (Exception ex)
                {
                    Log.Error($"Ошибка при отправке сообщения в Discord: {ex.Message}");
                }
            }


            // Удаляем опасные символы
            string sanitizedMessage = Regex.Replace(ev.Message, @"[<>@#\*\\\|`]", "");

            // Игнорируем пустые/слишком длинные сообщения
            if (string.IsNullOrEmpty(sanitizedMessage) || sanitizedMessage.Length > 200)
                return;


            payload = new WebhookPayload
            {
                Content = sanitizedMessage,
            };
        }
    }
}