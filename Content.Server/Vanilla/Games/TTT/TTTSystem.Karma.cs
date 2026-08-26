using Content.Shared.Vanilla.Games.TTT;
using System.IO;
using System.Text.Json;
using Robust.Shared.Network;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Prototypes;
using Content.Shared.Damage.Prototypes;

namespace Content.Server.Vanilla.Games.TTT;

public sealed partial class TTTSystem : SharedTTTSystem
{
    [Dependency] private MobStateSystem _mob = default!;
    [Dependency] private ILogManager _log = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!; // Добавьте это

    private const float KarmaRatio = 0.002f;
    private const float TraitorDamageRatio = 0.0003f;
    private const float KarmaRoundIncrement = 5f;
    private const float KarmaCleanBonus = 5f;
    private const float KarmaMax = 1000f;
    private const float DefaultKarma = 1000f;

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

        if (IsSameTeam(attackerRole, victimRole))
        {
            var karmaLoss = damage * victimKarma * KarmaRatio;
            AddKarma(sourceComp.Session.UserId, -karmaLoss);
            sourceComp.TeamKiller = true;
            return;
        }

        if (attackerRole is TTTRole.Inocent or TTTRole.Detective &&
            victimRole == TTTRole.Traitor)
        {
            var karmaGain = damage * victimKarma * TraitorDamageRatio;
            AddKarma(sourceComp.Session.UserId, karmaGain);
        }
    }

    private void OnDamageModify(EntityUid uid, TTTMarkerComponent component, DamageModifyEvent args)
    {
        if (!TryComp<TTTMarkerComponent>(args.Origin, out var sourcecomp) || args.Origin == uid)
            return;

        if (sourcecomp.Role == TTTRole.Await)
        {
            args.Damage = new DamageSpecifier();
            return;
        }

        // У детектива 30% резиста
        if (component.Role == TTTRole.Detective)
        {
            const float detectiveResist = 0.3f;

            // Создаем словарь с типами урона
            var coefficients = new Dictionary<ProtoId<DamageTypePrototype>, float>();

            // Получаем все типы урона из прототипов
            var damageTypes = _prototypeManager.EnumeratePrototypes<DamageTypePrototype>();
            foreach (var damageType in damageTypes)
            {
                coefficients[damageType.ID] = detectiveResist;
            }

            var detectiveModify = new DamageModifierSet
            {
                Coefficients = coefficients
            };
            args.Damage = DamageSpecifier.ApplyModifierSet(args.Damage, detectiveModify);
        }

        // Применяем модификатор урона на основе кармы
        var attackerKarma = GetKarma(sourcecomp.Session.UserId);
        var karmaFraction = Math.Clamp(attackerKarma / 1000f, 0f, 1f);

        var karmaCoefficients = new Dictionary<ProtoId<DamageTypePrototype>, float>();
        var allDamageTypes = _prototypeManager.EnumeratePrototypes<DamageTypePrototype>();
        foreach (var damageType in allDamageTypes)
        {
            karmaCoefficients[damageType.ID] = karmaFraction;
        }

        var karmaModify = new DamageModifierSet
        {
            Coefficients = karmaCoefficients
        };
        args.Damage = DamageSpecifier.ApplyModifierSet(args.Damage, karmaModify);
    }
}
