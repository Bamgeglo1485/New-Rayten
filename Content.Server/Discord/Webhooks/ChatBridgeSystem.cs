using Content.Server.Discord;
using Content.Server.Administration.Managers;
using Content.Server.Chat.Systems;
using Content.Shared.Chat;
using Content.Shared.Vanilla.Anticheat;
using Content.Shared.Vanilla.CCVars;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Configuration;

using System.Text.RegularExpressions;

public sealed class DiscordChatRelaySystem : EntitySystem
{
    [Dependency] private readonly DiscordWebhook _discordWebhook = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IAdminManager _adminManager = default!;
    private string _webhookUrl = "";
    private string _anticheatwebhookUrl = "";

    private TimeSpan? NextTime;
    private  WebhookPayload? payload;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<EntitySpokeEvent>(OnEntitySpoke);
        SubscribeNetworkEvent<SuspiciousClientEvent>(OnAntiCheatEvent);
        _cfg.OnValueChanged(CCVVars.DiscordBridgeWebhook, v => _webhookUrl = v, true);
        _cfg.OnValueChanged(CCVVars.DiscordAntiCheatWebhook, v => _anticheatwebhookUrl = v, true);
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

    private async void OnAntiCheatEvent(SuspiciousClientEvent ev, EntitySessionEventArgs args)
    {
        if (string.IsNullOrWhiteSpace(_anticheatwebhookUrl))
            return;

        var session = args.SenderSession;

        // if (_adminManager.IsAdmin(session))
        //     return;

        try
        {
            var (id, token) = ParseWebhookUrl(_anticheatwebhookUrl);

            string ckey = session.Name;
            string userId = session.UserId.ToString();
            string reason = ev.Reason ?? "не указана";

            string entityName = "Неизвестный объект";

            if (TryComp<MetaDataComponent>(session.AttachedEntity, out var meta))
            {
                entityName = meta.EntityName;
            }

            var content = $"🚨 **Обнаружен подозрительный клиент!**\n" +
                        $"**Имя объекта:** `{entityName}`\n" +
                        $"**CKey:** `{ckey}`\n" +
                        $"**UserId:** `{userId}`\n" +
                        $"**Причина:** `{reason}`";

            var payload = new WebhookPayload
            {
                Content = content
            };

            await _discordWebhook.CreateMessage(new WebhookIdentifier(id, token), payload);
        }
        catch (Exception ex)
        {
            Log.Error($"[Античит] Ошибка при отправке вебхука: {ex.Message}");
        }
    }


    private async void OnEntitySpoke(EntitySpokeEvent ev)
    {
        if (_webhookUrl == "")
            return;
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
                    NextTime = _timing.CurTime + TimeSpan.FromSeconds(_random.NextFloat(120, 300));
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
