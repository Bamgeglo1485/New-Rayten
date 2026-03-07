using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Robust.Shared.Network;
using Timer = Robust.Shared.Timing.Timer;
namespace Content.Server.Vanilla.TDM;

public sealed partial class TDMSystem : EntitySystem
{
    private Dictionary<NetUserId, int> _mmr = [];
    private const string LadderPath = "tdm_ladder.json";
    private void LoadMMR()
    {
        if (!File.Exists(LadderPath))
            return;

        var json = File.ReadAllText(LadderPath);
        _mmr = JsonSerializer.Deserialize<Dictionary<NetUserId, int>>(json) ?? new();
    }
    private void SaveMMR()
    {
        var json = JsonSerializer.Serialize(_mmr, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        File.WriteAllText(LadderPath, json);
    }

    public int GetMMR(NetUserId id)
    {
        return _mmr.TryGetValue(id, out var mmr) ? mmr : 1000;
    }
    public void AddMMR(NetUserId id, int value)
    {
        _mmr[id] = GetMMR(id) + value;
    }

}
