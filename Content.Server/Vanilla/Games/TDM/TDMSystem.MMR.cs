using System.IO;
using System.Text.Json;
using Robust.Shared.Network;

namespace Content.Server.Vanilla.TDM;

public sealed partial class TDMSystem : EntitySystem
{
    private Dictionary<string, int> _mmr = new();
    private const string LadderPath = "tdm_ladder.json";

    private void LoadMMR()
    {
        if (!File.Exists(LadderPath))
            return;

        var json = File.ReadAllText(LadderPath);
        _mmr = JsonSerializer.Deserialize<Dictionary<string, int>>(json) ?? new();
    }

    private void SaveMMR()
    {
        try
        {
            var json = JsonSerializer.Serialize(_mmr, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            Logger.Info($"Сохраняем MMR: {Path.GetFullPath(LadderPath)}");

            File.WriteAllText(LadderPath, json);
        }
        catch (Exception e)
        {
            Logger.Error($"Ошибка сохранения MMR: {e}");
        }
    }

    public int GetMMR(NetUserId id)
    {
        return _mmr.TryGetValue(id.ToString(), out var mmr) ? mmr : 1000;
    }

    public void AddMMR(NetUserId id, int value)
    {
        _mmr[id.ToString()] = GetMMR(id) + value;
    }
}
