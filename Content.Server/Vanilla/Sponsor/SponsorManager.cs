using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Content.Shared.Vanilla.CCVars;
using Content.Shared.Vanilla.Sponsor;
using JetBrains.Annotations;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;
namespace Content.Server.Vanilla.Sponsor;

public sealed class SponsorManager
{
    [Dependency] private readonly IPlayerManager _playerMgr = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IServerNetManager _netMgr = default!;
    [Dependency] private readonly SharedSponsorManager _sharedSponsorManager = default!; // Внедрение SharedSponsorManager

    private ISawmill _sawmill = default!;
    private readonly HttpClient _httpClient = new();
    private bool _isEnabled = false;
    private string _apiUrl = string.Empty;

    public void Initialize()
    {
        _sawmill = Logger.GetSawmill("СПОНСОРКА");
        _cfg.OnValueChanged(CCVVars.SponsorEnabled, v => _isEnabled = v, true);
        _cfg.OnValueChanged(CCVVars.SponsorApiUrl, v => _apiUrl = v, true);
        _playerMgr.PlayerStatusChanged += OnPlayerStatusChanged;

        _netMgr.RegisterNetMessage<SetSponsorRank>();
    }

    public async void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs e)
    {
        if (!_isEnabled)
            return;

        if (e.NewStatus == SessionStatus.Connected)
        {
            sponsorRank SponsorRank = await GetSponsorRank(e.Session.UserId);
            _sawmill.Info($"У пользователя {e.Session.UserId} вот такой ранг: {SponsorRank}");
            // Отправляем сетевое сообщение
            var msg = new SetSponsorRank
            {
                rank = SponsorRank
            };
            e.Session.Channel.SendMessage(msg);

            _sharedSponsorManager.ServerSponsorSet(e.Session.UserId, SponsorRank, false); 
        }

        if (e.NewStatus == SessionStatus.Disconnected)
        {
            _sawmill.Info($"Ранг пользователя {e.Session.UserId} удалён при отключении.");
            var msg = new SetSponsorRank
            {
                rank = sponsorRank.None
            };
            e.Session.Channel.SendMessage(msg);
            _sharedSponsorManager.ServerSponsorSet(e.Session.UserId, sponsorRank.None, true);
        }
    }


    public async Task<sponsorRank> GetSponsorRank(NetUserId userId, CancellationToken cancel = default)
    {
        var requestUrl = $"{_apiUrl}/{WebUtility.UrlEncode(userId.ToString())}/sponsor";//https://example.ru/1231-1323-3123-fasd/sponsor
        var response = await _httpClient.GetAsync(requestUrl, cancel);
        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            throw new Exception($"Verification API returned bad status code: {response.StatusCode}\nResponse: {content}");
        }
        var data = await response.Content.ReadFromJsonAsync<SponsorInfoResponse>(cancellationToken: cancel);
        return ParseSponsorRank(data!.rank);
    }


    private sponsorRank ParseSponsorRank(string rank)
    {
        return rank switch
        {
            "None" => sponsorRank.None,
            "GrayTide" =>  sponsorRank.GrayTide,
            "Revolutionrevolutionary" =>  sponsorRank.Revolutionary,
            "Syndicate" =>  sponsorRank.Syndicate,
            "SpaceNinja" =>  sponsorRank.SpaceNinja,
            _ => sponsorRank.None
        };
    }

    [UsedImplicitly]
    private sealed record SponsorInfoResponse(string rank);
}

