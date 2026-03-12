using Content.Shared.Vanilla.Games.TTT;
using System.IO;
using System.Text.Json;
using Robust.Shared.Network;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs.Systems;
namespace Content.Server.Vanilla.Games.TTT;

public sealed partial class TTTSystem : SharedTTTSystem
{
    [Dependency] private readonly MobStateSystem _mob = default!;
    [Dependency] private readonly ILogManager _log = default!;
    private const float KarmaRatio = 0.002f;//модификатор кармы за тимкилл (-)
    private const float TraitorDamageRatio = 0.0003f;//модификатор кармы за урон по предателю (+)
    private const float KarmaRoundIncrement = 5f;//карма в конце раунда
    private const float KarmaCleanBonus = 5f;//дополнительная карма в конце раунда, если игрок не стрелял в союзников.
    private const float KarmaMax = 1000f;//максимальная карма
    private const float DefaultKarma = 1000f;//дефолтное значение кармы

    private Dictionary<string, float> _karma = new();
    private const string LadderPath = "ttt_karma.json";
    private void LoadKarma()
    {
        if (!File.Exists(LadderPath))
            return;

        var json = File.ReadAllText(LadderPath);
        _karma = JsonSerializer.Deserialize<Dictionary<string, float>>(json) ?? new();
    }

    private void SaveKarma()
    {
        var sawmill = _log.GetSawmill("карма");
        try
        {
            var json = JsonSerializer.Serialize(_karma, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            sawmill.Info($"Сохраняем Карму: {Path.GetFullPath(LadderPath)}");

            File.WriteAllText(LadderPath, json);
        }
        catch (Exception e)
        {
            sawmill.Error($"Ошибка сохранения кармы: {e}");
        }
    }

    public float GetKarma(NetUserId id)
    {
        return _karma.TryGetValue(id.ToString(), out var karma) ? karma : DefaultKarma;
    }

    public void AddKarma(NetUserId id, float value)
    {
        _karma[id.ToString()] = Math.Clamp(GetKarma(id) + value, -1f, KarmaMax);
    }

    /// <summary>
    /// уменьшаем карму в зависимости от нанесённого урона
    /// </summary>
    private void OnDamageChange(EntityUid uid, TTTMarkerComponent component, DamageChangedEvent args)
    {
        if (!args.DamageIncreased
            || args.DamageDelta == null
            || args.DamageDelta.GetTotal() <= 0
            || args.Origin == uid
            || !TryComp<TTTMarkerComponent>(args.Origin, out var sourceComp)
            || !_mob.IsAlive(uid))
        {
            return;
        }

        var damage = args.DamageDelta.GetTotal().Float();

        var attackerRole = sourceComp.Role;
        var victimRole = component.Role;
        var victimKarma = GetKarma(component.Session.UserId);

        // Тимдамаг
        if (IsSameTeam(attackerRole, victimRole))
        {
            var karmaLoss = damage * victimKarma * KarmaRatio;
            AddKarma(sourceComp.Session.UserId, -karmaLoss);
            sourceComp.TeamKiller = true;
            return;
        }

        // Урон по предателю
        if (attackerRole is TTTRole.Inocent or TTTRole.Detective &&
            victimRole == TTTRole.Traitor)
        {
            var karmaGain = damage * victimKarma * TraitorDamageRatio;
            AddKarma(sourceComp.Session.UserId, karmaGain);
        }
    }

    //уменьшаем урон в зависимости от кармы
    private void OnDamageModify(EntityUid uid, TTTMarkerComponent component, DamageModifyEvent args)
    {
        if (!TryComp<TTTMarkerComponent>(args.Origin, out var sourcecomp) || args.Origin == uid)
            return;

        if (sourcecomp.Role == TTTRole.Await)
        {
            args.Damage = new DamageSpecifier();
            return;
        }
        ///у детектива 30% резиста
        if (component.Role == TTTRole.Detective)
        {
            const float detectiveResist = 0.3f;
            var detectiveModify = new DamageModifierSet
            {
                Coefficients = new Dictionary<string, float>
                {
                    ["Slash"] = detectiveResist,
                    ["Piercing"] = detectiveResist,
                    ["Blunt"] = detectiveResist,
                    ["Heat"] = detectiveResist,
                    ["Shock"] = detectiveResist,
                    ["Cold"] = detectiveResist,
                    ["Poison"] = detectiveResist,
                    ["Radiation"] = detectiveResist,
                    ["Asphyxiation"] = detectiveResist,
                    ["Bloodloss"] = detectiveResist
                }
            };
            args.Damage = DamageSpecifier.ApplyModifierSet(args.Damage, detectiveModify);
        }

        //  применяем модификатор урона на основе новой кармы
        var attackerKarma = GetKarma(sourcecomp.Session.UserId);
        var karmaFraction = Math.Clamp(attackerKarma / 1000f, 0f, 1f);

        var modify = new DamageModifierSet
        {
            Coefficients = new Dictionary<string, float>
            {
                ["Slash"] = karmaFraction,
                ["Piercing"] = karmaFraction,
                ["Blunt"] = karmaFraction,
                ["Heat"] = karmaFraction,
                ["Shock"] = karmaFraction,
                ["Cold"] = karmaFraction,
                ["Poison"] = karmaFraction,
                ["Radiation"] = karmaFraction,
                ["Asphyxiation"] = karmaFraction,
                ["Bloodloss"] = karmaFraction
            }
        };
        args.Damage = DamageSpecifier.ApplyModifierSet(args.Damage, modify);
    }
}
