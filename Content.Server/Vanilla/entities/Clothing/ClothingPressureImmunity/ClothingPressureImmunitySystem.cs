using Content.Shared.Inventory.Events;
using Content.Server.Atmos.Components;

namespace Content.Server.Vanilla.Entities.ClothingPressureImmunity;

public sealed class ClothingPressureImmunitySystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<ClothingPressureImmunityComponent, GotEquippedEvent>(OnPressureProtectionEquipped);
        SubscribeLocalEvent<ClothingPressureImmunityComponent, GotUnequippedEvent>(OnPressureProtectionUnequipped);
    }

    private void OnPressureProtectionEquipped(EntityUid uid, ClothingPressureImmunityComponent pressureProtection, GotEquippedEvent args)
    {
        EnsureComp<PressureImmunityComponent>(args.Equipee);
    }

    private void OnPressureProtectionUnequipped(EntityUid uid, ClothingPressureImmunityComponent pressureProtection, GotUnequippedEvent args)
    {
        RemComp<PressureImmunityComponent>(args.Equipee);
    }
}