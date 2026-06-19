using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Robust.Shared.Random;
using Robust.Shared.Prototypes;
using System.Linq;
using Content.Shared.Damage;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Tag;
using Content.Shared.Damage.Systems;
using Content.Shared.Damage.Components;
using Content.Shared.Buckle.Components;
using Content.Shared.Movement.Events;
using Content.Shared.StatusEffect;
using Content.Shared.Standing;
using Content.Shared.Popups;
using Content.Shared.Stunnable;

namespace Content.Shared.Vanilla.CompoundZ;

public sealed partial class SharedSuperSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private TagSystem _tag = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedPopupSystem _popupSystem = default!;
    [Dependency] private SharedStunSystem _stun = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SuperComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<SuperComponent, ComponentShutdown>(OnSuperShutdown);
    }

    private void OnStartup(Entity<SuperComponent> entity, ref ComponentStartup args)
    {
        var superPrototype = entity.Comp.Prototype;
        // Выбираем случайный прототип суперспособности
        if (superPrototype == null)
        {
            var prototypes = _prototypeManager.EnumeratePrototypes<SuperAbilityPrototype>().ToList();
            if (prototypes.Count == 0)
                return;

            superPrototype = _random.Pick(prototypes);
            entity.Comp.Prototype = superPrototype;
        }

        // Добавляем компоненты
        if (superPrototype.Components != null && superPrototype.Components.Count > 0)
        {
            EntityManager.AddComponents(entity, superPrototype.Components, removeExisting: true);
        }

        // Добавляем неудаляемые компоненты
        if (superPrototype.UnremovableComponents != null && superPrototype.UnremovableComponents.Count > 0)
        {
            EntityManager.AddComponents(entity, superPrototype.UnremovableComponents, removeExisting: true);
        }

        // Добавляем акшены
        if (superPrototype.Actions != null && superPrototype.Actions.Length > 0)
        {
            if (!TryComp(entity, out ActionsComponent? actionsComp))
                return;

            entity.Comp.Actions = new EntityUid?[superPrototype.Actions.Length];

            for (int i = 0; i < superPrototype.Actions.Length; i++)
            {
                var actionId = superPrototype.Actions[i];

                _actions.AddAction(entity, ref entity.Comp.Actions[i], actionId, component: actionsComp);
            }
        }

        var ev = new SuperBornEvent();
        RaiseLocalEvent(entity.Owner, ev);
    }

    private void OnSuperShutdown(Entity<SuperComponent> entity, ref ComponentShutdown args)
    {
        // Удаляем акшены
        if (entity.Comp.Actions != null)
        {
            foreach (var actionEntity in entity.Comp.Actions)
            {
                if (actionEntity.HasValue)
                {
                    _actions.RemoveAction(entity.Owner, actionEntity.Value);
                }
            }
        }

        // Удаляем компоненты
        if (entity.Comp.Prototype?.Components != null && entity.Comp.Prototype.Components.Count > 0)
        {
            EntityManager.RemoveComponents(entity, entity.Comp.Prototype.Components);
        }

        var ev = new SuperLossEvent();
        RaiseLocalEvent(entity.Owner, ev);

        var time = TimeSpan.FromSeconds(3);
        _stun.TryAddParalyzeDuration(entity.Owner, time);
    }

    public void GrantSuperAbility(EntityUid entity, string prototypeId)
    {
        if (!_prototypeManager.TryIndex<SuperAbilityPrototype>(prototypeId, out var prototype))
            return;

        if (HasComp<SuperComponent>(entity))
            return;

        var superComp = new SuperComponent
        {
            Prototype = prototype
        };

        AddComp(entity, superComp);
    }
}
