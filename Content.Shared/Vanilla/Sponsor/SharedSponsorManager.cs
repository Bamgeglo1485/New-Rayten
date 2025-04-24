using System.Collections.Generic;
using Robust.Shared.Network;
using Robust.Shared.IoC;
using Robust.Shared.Log;

namespace Content.Shared.Vanilla.Sponsor;

public sealed class SharedSponsorManager
{
    [Dependency] private readonly INetManager _net = default!;
    private readonly Dictionary<NetUserId, sponsorRank> _ranks = new();
    private sponsorRank _Clientrank = sponsorRank.None;
    public void Initialize()
    {
        if (_net.IsClient)
        {
            _net.RegisterNetMessage<SetSponsorRank>(OnClientSponsorSet);
        }
    }

    public void ServerSponsorSet(NetUserId userId, sponsorRank rank, bool remove)
    {
        Logger.Info($"Вызван метод");
        
        if(remove)
        {
            _ranks.Remove(userId);
        }
        else
        {
            _ranks[userId] = rank;
            Logger.Info($"[Server] Обновлен спонсорский ранг {rank} для {userId}");
        }
    }

    private void OnClientSponsorSet(SetSponsorRank message)
    {
        _Clientrank = message.rank;
        Logger.Info($"[Client] Получен спонсорский ранг {message.rank}");
    }

    public bool TryGetOOCColor(NetUserId userId, out string oocColor)
    {
        if (_net.IsClient)
        {
            oocColor = "white";
            return false;
        }

        if (!_ranks.TryGetValue(userId, out var rank))
        {
            oocColor = "white";
            return false;
        }

        oocColor = rank switch
        {
            sponsorRank.GrayTide => "#546E7A",
            sponsorRank.Revolutionary => "#33CCEA",
            sponsorRank.Syndicate => "#990000",
            sponsorRank.SpaceNinja => "#1ABC9C",
            _ => "white"
        };
        return rank != sponsorRank.None;
    }
    public IReadOnlyList<string> GetClientPrototypes()
    {
        if (_Clientrank == sponsorRank.None)
            return Array.Empty<string>();

        return getprotos(_Clientrank);
    }

    public bool TryGetServerPrototypes(NetUserId userId, out string[] prototypes)
    {
        if (!_ranks.TryGetValue(userId, out var rank))
        {
            prototypes = Array.Empty<string>(); 
            return false;
        }

        prototypes = getprotos(rank);
        return true;
    }
    public int GetServerExtraCharSlots(NetUserId userId)
    {
        int slots = 0;

        if (!_ranks.TryGetValue(userId, out var rank))
            return slots;

        if (rank >= sponsorRank.Revolutionary)
            slots = 10;

        return slots;
    }

    private string[] getprotos(sponsorRank rank)
    {
        List<string> prototypes = new();

        if (rank >= sponsorRank.GrayTide)
            prototypes.Add("gray_voice");

        if (rank >= sponsorRank.Revolutionary)
            prototypes.Add("revo_voice");

        if (rank >= sponsorRank.Syndicate)
            prototypes.Add("Megalovania");

        if (rank >= sponsorRank.SpaceNinja)
            prototypes.Add("ninja_voice");

        // Преобразуем список в массив и возвращаем его.
        return prototypes.ToArray();
    }

}
