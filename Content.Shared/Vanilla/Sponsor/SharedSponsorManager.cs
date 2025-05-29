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
    public string[] GetClientPrototypes()
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

        if (rank >= sponsorRank.GrayTide)
            slots += 5;

        if (rank >= sponsorRank.Revolutionary)
            slots += 5;

        return slots;
    }
    #endregion
    #region установка словарей
    public void ServerSponsorSet(NetUserId userId, sponsorRank rank, bool remove)
    {
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
                //прически
                current.Add("HumanHairCotton");
                current.Add("HumanHairFingerwave");
                current.Add("HumanHairFortuneteller");
                current.Add("HumanHairFortunetellerAlt");
                current.Add("HumanHairLongdtails");
                current.Add("HumanHairLooseSlicked");
                current.Add("HumanHairQuadcurls");
                current.Add("HumanHairShy");
                current.Add("HumanHairSpicy");
                current.Add("HumanHairWife");
                current.Add("HumanHairNitori");
                current.Add("HumanHairLongBow");
                //голоса
                current.Add("Chingchong");
                //импланты
                current.Add("CyberlimbRArmBishop");current.Add("CyberlimbLArmBishop");current.Add("CyberlimbRHandBishop");
                current.Add("CyberlimbLHandBishop");current.Add("CyberlimbRLegBishop");current.Add("CyberlimbLLegBishop");
                current.Add("CyberlimbLFootBishop");current.Add("CyberlimbRFootBishop");current.Add("CyberlimbTorsoBishop");
                current.Add("CyberlimbRArmHephaestus");current.Add("CyberlimbRHandHephaestus");current.Add("CyberlimbLHandHephaestus");
                current.Add("CyberlimbRLegHephaestus");current.Add("CyberlimbLLegHephaestus");
                current.Add("CyberlimbLFootHephaestus");current.Add("CyberlimbRFootHephaestus");current.Add("CyberlimbTorsoHephaestus");
                current.Add("CyberlimbRArmHephaestusTitan");current.Add("CyberlimbLArmHephaestusTitan");current.Add("CyberlimbRHandHephaestusTitan");
                current.Add("CyberlimbLHandHephaestusTitan");current.Add("CyberlimbRLegHephaestusTitan");current.Add("CyberlimbLLegHephaestusTitan");
                current.Add("CyberlimbLFootHephaestusTitan");current.Add("CyberlimbRFootHephaestusTitan");current.Add("CyberlimbTorsoHephaestusTitan");
                current.Add("CyberlimbRArmMorpheus");current.Add("CyberlimbLArmMorpheus");current.Add("CyberlimbRHandMorpheus");current.Add("CyberlimbLHandMorpheus");
                current.Add("CyberlimbRLegMorpheus");current.Add("CyberlimbLLegMorpheus");current.Add("CyberlimbLFootMorpheus");
                current.Add("CyberlimbRFootMorpheus");current.Add("CyberlimbTorsoMorpheus");current.Add("CyberlimbRArmWardtakahashi");
                current.Add("CyberlimbLArmWardtakahashi");current.Add("CyberlimbRHandWardtakahashi");current.Add("CyberlimbLHandWardtakahashi");
                current.Add("CyberlimbRLegWardtakahashi");current.Add("CyberlimbLLegWardtakahashi");current.Add("CyberlimbLFootWardtakahashi");
                current.Add("CyberlimbRFootWardtakahashi");current.Add("CyberlimbTorsoWardtakahashiMale");current.Add("CyberlimbTorsoWardtakahashiFemale");
                current.Add("CyberlimbRArmZenghu");current.Add("CyberlimbLArmZenghu");current.Add("CyberlimbRHandZenghu");
                current.Add("CyberlimbLHandZenghu");current.Add("CyberlimbRLegZenghu");current.Add("CyberlimbLLegZenghu");
                current.Add("CyberlimbLFootZenghu");current.Add("CyberlimbRFootZenghu");current.Add("CyberlimbTorsoZenghu");
                current.Add("CyberlimbRArmNanotrasen");current.Add("CyberlimbLArmNanotrasen");current.Add("CyberlimbRHandNanotrasen");
                current.Add("CyberlimbLHandNanotrasen");current.Add("CyberlimbRLegNanotrasen");current.Add("CyberlimbLLegNanotrasen");
                current.Add("CyberlimbLFootNanotrasen");current.Add("CyberlimbRFootNanotrasen");current.Add("CyberlimbTorsoNanotrasen");
                current.Add("CyberlimbRArmXion");current.Add("CyberlimbLArmXion");current.Add("CyberlimbRHandXion");
                current.Add("CyberlimbLHandXion");current.Add("CyberlimbRLegXion");current.Add("CyberlimbTorsoXion");
                current.Add("CyberlimbLLegXion"); current.Add("CyberlimbLFootXion");current.Add("CyberlimbRFootXion");
                    break;


                case sponsorRank.Syndicate:
                //кошачие хвосты и прочая срань
                    current.Add("HumanFoxTailAnimated");current.Add("CatEars");current.Add("CatTail");
                    current.Add("SlimeCatTailStripes");current.Add("SlimeCatTail");current.Add("SlimeCatEarsTorn");
                    current.Add("SlimeCatEarsCurled");current.Add("SlimeCatEarsStubby");current.Add("SlimeCatEars");
                    current.Add("SlimeFoxEars");current.Add("CatEarsStubby");current.Add("CatEarsCurled");
                    current.Add("CatEarsTorn");current.Add("CatTailStripes");current.Add("FoxEars");
                    current.Add("HumanFoxTailAnimated");
                //голоса
                    current.Add("Willow");
                    current.Add("Warly");
                    current.Add("Megalovania");
                //предыстории
                    current.Add("CadetWizardBackground");
                    break;


                case sponsorRank.SpaceNinja:
                    current.Add("Meme");
                    break;
            }
            _rankToPrototypes[rank] = current.ToArray();
        }
    }
    #endregion
}
