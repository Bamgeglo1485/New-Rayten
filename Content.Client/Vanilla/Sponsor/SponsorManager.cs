// using System.Threading;
// using Robust.Shared.Network;
// using Content.Shared.Vanilla.Sponsor;

// namespace Content.Client.Vanilla.Sponsor;

// public sealed class SponsorManager
// {
//     [Dependency] private readonly IClientNetManager _netManager = default!;
//     private sponsorRank _rank = sponsorRank.None;

//     public void Initialize()
//     {
//         _netManager.RegisterNetMessage<SetSponsorRank>(OnSponsorSet);
//     }
//     private void OnSponsorSet(SetSponsorRank message)
//     {
//         _rank = message.rank;
//         Logger.Info($"[Client] Получен спонсорский ранг: {_rank}");
//     }
//     /// <summary>
//     /// Возвращает список ID прототипов, доступных текущему рангу.
//     /// Используется для, например, проверки доступных голосов.
//     /// </summary>
//     public IReadOnlyList<string> GetPrototypes()
//     {
//         List<string> prototypes = new();

//         // if (_rank >= sponsorRank.GrayTide)
//         //     prototypes.Add("gray_voice");

//         // if (_rank >= sponsorRank.Revolutionrevolutionary)
//         //     prototypes.Add("revo_voice");

//         if (_rank >= sponsorRank.Syndicate)
//             prototypes.Add("Megalovania");

//         // if (_rank >= sponsorRank.SpaceNinja)
//         //     prototypes.Add("ninja_voice");

//         return prototypes;
//     }

// }