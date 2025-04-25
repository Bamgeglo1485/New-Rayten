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
    private readonly Dictionary<sponsorRank, string[]> _rankToPrototypes = new();
    public void Initialize()
    {
        if (_net.IsClient)
        {
            _net.RegisterNetMessage<SetSponsorRank>(OnClientSponsorSet);
        }
        buildmap();
    }

    #region АПИШКИ
    public IReadOnlyList<string> GetClientPrototypes()
    {
        return GetPrototypesForRank(_Clientrank);
    }

    public bool TryGetServerPrototypes(NetUserId userId, out string[] prototypes)
    {
        if (!_ranks.TryGetValue(userId, out var rank))
        {
            prototypes = Array.Empty<string>(); 
            return false;
        }

        prototypes = GetPrototypesForRank(rank);
        return true;
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
    public int GetServerExtraCharSlots(NetUserId userId)
    {
        int slots = 0;

        if (!_ranks.TryGetValue(userId, out var rank))
            return slots;

        if (rank >= sponsorRank.Revolutionary)
            slots = 10;

        return slots;
    }
    #endregion
    #region установка словарей
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
    private string[] GetPrototypesForRank(sponsorRank rank)
    {
        return _rankToPrototypes.TryGetValue(rank, out var protos) ? protos : Array.Empty<string>();
    }
    #endregion
    #region Список всех доступных прототипов
    private void buildmap()
    {
        List<string> current = new();

        foreach (sponsorRank rank in Enum.GetValues(typeof(sponsorRank)))
        {
            switch (rank)
            {
                case sponsorRank.GrayTide:
                    break;
                case sponsorRank.Revolutionary:
                    current.Add("CatEars");
                    current.Add("CatTail");
                    break;
                case sponsorRank.Syndicate:
                    current.Add("Willow");
                    current.Add("WX");
                    break;
                case sponsorRank.SpaceNinja:
                    current.Add("Megalovania");
                    current.Add("Walany");
                    current.Add("Warly");
                    break;
            }
            _rankToPrototypes[rank] = current.ToArray();
        }
    }
    #endregion
}
