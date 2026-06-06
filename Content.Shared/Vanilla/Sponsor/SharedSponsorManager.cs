using System.Linq;
using Robust.Shared.Network;

namespace Content.Shared.Vanilla.Sponsor;

public sealed partial class SharedSponsorManager
{
    [Dependency] private INetManager _net = default!;
    [Dependency] private ILogManager _logManager = default!;
    private ISawmill _sawmill = default!;
    private readonly Dictionary<NetUserId, sponsorRank> _ranks = [];
    private sponsorRank _clientrank = sponsorRank.None;
    private readonly Dictionary<sponsorRank, HashSet<string>> _rankToPrototypes = [];


    public void Initialize()
    {
        _sawmill = _logManager.GetSawmill("Спонсорская система:");
        if (_net.IsClient)
            _net.RegisterNetMessage<SetSponsorRank>(OnClientSponsorSet);
        Buildmap();
    }

    #region АПИШКИ
    public HashSet<string> GetSponsorPrototypes()
    {
        return GetPrototypesForRank(_clientrank);
    }

    public HashSet<string> GetSponsorPrototypes(NetUserId userId)
    {
        if (_net.IsClient)
            return GetSponsorPrototypes();

        return _ranks.TryGetValue(userId, out var rank) ? GetPrototypesForRank(rank) : [];
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
            sponsorRank.Syndicate => "#880808",
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
    public void ServerSponsorSet(NetUserId userId, sponsorRank rank)
    {
        _ranks[userId] = rank;
    }

    private void OnClientSponsorSet(SetSponsorRank message)
    {
        _clientrank = message.rank;
        _sawmill.Info($"Получен спонсорский ранг {message.rank}, доступные прототипы:");

        var protos = GetSponsorPrototypes().ToList();

        for (var i = 0; i < protos.Count; i += 5)
        {
            var chunk = protos.Skip(i).Take(5);
            _sawmill.Info(string.Join(", ", chunk));
        }
    }
    private HashSet<string> GetPrototypesForRank(sponsorRank rank)
    {
        return _rankToPrototypes.TryGetValue(rank, out var protos) ? protos : []; //HashSet<string>
    }
    #endregion
    #region Список всех доступных прототипов
    private void Buildmap()
    {
        HashSet<string>? previous = null;
        foreach (var rank in Enum.GetValues<sponsorRank>())
        {
            HashSet<string> current = [];
            // добавляем всё из предыдущего ранга
            if (previous != null)
                current.UnionWith(previous);
            switch (rank)
            {
                case sponsorRank.GrayTide:
                    current.Add("ClosetSkeletonJesterBackground");
                    current.Add("NukeOpfreelancerBackground");
                    current.Add("BlueGuySpyBackground");
                    current.Add("RedGuySpyBackground");
                    current.Add("Trottine");
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
                    current.Add("CyberlimbRArmBishop"); current.Add("CyberlimbLArmBishop"); current.Add("CyberlimbRHandBishop");
                    current.Add("CyberlimbLHandBishop"); current.Add("CyberlimbRLegBishop"); current.Add("CyberlimbLLegBishop");
                    current.Add("CyberlimbLFootBishop"); current.Add("CyberlimbRFootBishop"); current.Add("CyberlimbTorsoBishop");
                    current.Add("CyberlimbRArmHephaestus"); current.Add("CyberlimbRHandHephaestus"); current.Add("CyberlimbLHandHephaestus");
                    current.Add("CyberlimbRLegHephaestus"); current.Add("CyberlimbLLegHephaestus");
                    current.Add("CyberlimbLFootHephaestus"); current.Add("CyberlimbRFootHephaestus"); current.Add("CyberlimbTorsoHephaestus");
                    current.Add("CyberlimbRArmHephaestusTitan"); current.Add("CyberlimbLArmHephaestusTitan"); current.Add("CyberlimbRHandHephaestusTitan");
                    current.Add("CyberlimbLHandHephaestusTitan"); current.Add("CyberlimbRLegHephaestusTitan"); current.Add("CyberlimbLLegHephaestusTitan");
                    current.Add("CyberlimbLFootHephaestusTitan"); current.Add("CyberlimbRFootHephaestusTitan"); current.Add("CyberlimbTorsoHephaestusTitan");
                    current.Add("CyberlimbRArmMorpheus"); current.Add("CyberlimbLArmMorpheus"); current.Add("CyberlimbRHandMorpheus"); current.Add("CyberlimbLHandMorpheus");
                    current.Add("CyberlimbRLegMorpheus"); current.Add("CyberlimbLLegMorpheus"); current.Add("CyberlimbLFootMorpheus");
                    current.Add("CyberlimbRFootMorpheus"); current.Add("CyberlimbTorsoMorpheus"); current.Add("CyberlimbRArmWardtakahashi");
                    current.Add("CyberlimbLArmWardtakahashi"); current.Add("CyberlimbRHandWardtakahashi"); current.Add("CyberlimbLHandWardtakahashi");
                    current.Add("CyberlimbRLegWardtakahashi"); current.Add("CyberlimbLLegWardtakahashi"); current.Add("CyberlimbLFootWardtakahashi");
                    current.Add("CyberlimbRFootWardtakahashi"); current.Add("CyberlimbTorsoWardtakahashiMale"); current.Add("CyberlimbTorsoWardtakahashiFemale");
                    current.Add("CyberlimbRArmZenghu"); current.Add("CyberlimbLArmZenghu"); current.Add("CyberlimbRHandZenghu");
                    current.Add("CyberlimbLHandZenghu"); current.Add("CyberlimbRLegZenghu"); current.Add("CyberlimbLLegZenghu");
                    current.Add("CyberlimbLFootZenghu"); current.Add("CyberlimbRFootZenghu"); current.Add("CyberlimbTorsoZenghu");
                    current.Add("CyberlimbRArmNanotrasen"); current.Add("CyberlimbLArmNanotrasen"); current.Add("CyberlimbRHandNanotrasen");
                    current.Add("CyberlimbLHandNanotrasen"); current.Add("CyberlimbRLegNanotrasen"); current.Add("CyberlimbLLegNanotrasen");
                    current.Add("CyberlimbLFootNanotrasen"); current.Add("CyberlimbRFootNanotrasen"); current.Add("CyberlimbTorsoNanotrasen");
                    current.Add("CyberlimbRArmXion"); current.Add("CyberlimbLArmXion"); current.Add("CyberlimbRHandXion");
                    current.Add("CyberlimbLHandXion"); current.Add("CyberlimbRLegXion"); current.Add("CyberlimbTorsoXion");
                    current.Add("CyberlimbLLegXion"); current.Add("CyberlimbLFootXion"); current.Add("CyberlimbRFootXion");
                    break;


                case sponsorRank.Syndicate:
                    //кошачие хвосты и прочая срань
                    current.Add("HumanFoxTailAnimated"); current.Add("CatEars"); current.Add("CatTail");
                    current.Add("SlimeCatTailStripes"); current.Add("SlimeCatTail"); current.Add("SlimeCatEarsTorn");
                    current.Add("SlimeCatEarsCurled"); current.Add("SlimeCatEarsStubby"); current.Add("SlimeCatEars");
                    current.Add("SlimeFoxEars"); current.Add("CatEarsStubby"); current.Add("CatEarsCurled");
                    current.Add("CatEarsTorn"); current.Add("CatTailStripes"); current.Add("FoxEars");
                    //голоса
                    current.Add("Willow");
                    current.Add("Warly");
                    current.Add("Megalovania");
                    break;


                case sponsorRank.SpaceNinja:
                    current.Add("Meme");
                    break;
            }
            previous = current;
            _rankToPrototypes[rank] = current;
        }
    }
    #endregion
}
