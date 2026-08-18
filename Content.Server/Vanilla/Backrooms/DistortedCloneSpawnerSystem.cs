using Robust.Shared.Prototypes;
using Robust.Shared.Containers;
using Robust.Shared.Random;

using Content.Server.Cloning.Components;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Server.Cloning;

using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Content.Shared.Inventory;
using Content.Shared.Damage;
using Content.Shared.Sprite;
using Content.Shared.Body.Components;
using Content.Shared.SSDIndicator;
using Content.Shared.Mind;
using Content.Shared.Objectives.Systems;
using Content.Shared.Interaction.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Mobs;

using System.Numerics;


namespace Content.Server.Vanilla.Backrooms;

public sealed partial class DistortedCloneSpawnerSystem : EntitySystem
{
    [Dependency] private CloningSystem _cloning = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedTransformSystem _transformSystem = default!;
    [Dependency] private SharedScaleVisualsSystem ScaleVisuals = default!;
    [Dependency] private HTNSystem _htn = default!;
    [Dependency] private NpcFactionSystem _npcFaction = default!;
    [Dependency] private NPCSystem _npc = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private MobThresholdSystem _mobState = default!;
    [Dependency] private AliveHumanoidTargetSystem _target = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DistortedCloneSpawnerComponent, ComponentStartup>(OnMapInit);
    }

    private void OnMapInit(Entity<DistortedCloneSpawnerComponent> ent, ref ComponentStartup args)
    {
        QueueDel(ent.Owner);

        if (!_prototypeManager.TryIndex(ent.Comp.Settings, out var settings))
        {
            Log.Error($"Used invalid cloning settings {ent.Comp.Settings} for DistortedCloneSpawner");
            return;
        }

        var allHumans = _target.GetAliveHumans();

        if (allHumans.Count == 0)
            return;

        var bodyToClone = _random.Pick(allHumans).Comp.OwnedEntity;

        if (bodyToClone == null)
            return;

        _cloning.TryCloning(bodyToClone.Value, _transformSystem.GetMapCoordinates(ent.Owner), settings, out var clone);

        if (clone == null)
            return;

        if (HasComp<BloodstreamComponent>(clone.Value))
            RemComp<BloodstreamComponent>(clone.Value);

        if (HasComp<SSDIndicatorComponent>(clone.Value))
            RemComp<SSDIndicatorComponent>(clone.Value);

        var scale = new Vector2(_random.NextFloat(0.7f, 2.0f), _random.NextFloat(0.7f, 2.0f));
        ScaleVisuals.SetSpriteScale(clone.Value, scale);

        var npcFaction = EnsureComp<NpcFactionMemberComponent>(clone.Value);
        _npcFaction.ClearFactions((clone.Value, npcFaction), false);
        _npcFaction.AddFaction((clone.Value, npcFaction), "AllHostile");

        EnsureComp<HTNComponent>(clone.Value, out var htn);
        if (_random.Prob(ent.Comp.AgressiveChance))
            htn.RootTask = new HTNCompoundTask { Task = "SimpleHostileCompound" };
        else
            htn.RootTask = new HTNCompoundTask { Task = "MouseCompound" };
        htn.Blackboard.SetValue(NPCBlackboard.Owner, clone.Value);
        _npc.WakeNPC(clone.Value, htn);
        _htn.Replan(htn);

        var damage = new DamageSpecifier();
        damage.DamageDict.Add("Slash", 40);
        damage.DamageDict.Add("Burn", 40);
        _damageable.TryChangeDamage(clone.Value, damage);

        _mobState.SetMobStateThreshold(clone.Value, 400f, MobState.Dead);
        _mobState.SetMobStateThreshold(clone.Value, 350f, MobState.Critical);

        if (ent.Comp.Components.Count > 0)
            EntityManager.AddComponents(clone.Value, ent.Comp.Components, true);

        if (!TryComp<InventoryComponent>(clone.Value, out var inventoryComp))
            return;

        foreach (var slot in inventoryComp.Slots)
        {
            if (!_inventory.TryGetSlotEntity(clone.Value, slot.Name, out var item) || item == null)
                continue;

            EnsureComp<UnremoveableComponent>(item.Value);
        }
    }
}
