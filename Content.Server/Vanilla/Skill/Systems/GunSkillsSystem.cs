using System.Numerics;
using Content.Shared.Hands;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Components;
using Content.Shared.Item;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Damage.Components;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Vanilla.Skill;
using Content.Server.SkillTrainer;
using Content.Server.Hands.Systems;
using Content.Server.Vanilla.Skill;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Random;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Content.Shared.Projectiles;

namespace Content.Server.Vanilla.Skill;

public sealed class GunSkillsSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedGunSystem _gun = default!;
    [Dependency] private readonly HandsSystem _handsSystem = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly ServerSkillTrainerSystem _skillTrainerSystem = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedStaminaSystem _stamina = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GunComponent, GotEquippedHandEvent>(OnHandPickUp);//обновляет модификаторы при взятии оружия в руки
        SubscribeLocalEvent<GunComponent, GunRefreshModifiersEvent>(OnGunRefreshModifiers);//перегрузка обновления модификаторов
        SubscribeLocalEvent<GunCanBeFallComponent, GunShotEvent>(RangeWeaponFalldownOnShoot);//выпадение оружия при стрельбе
        SubscribeLocalEvent<ProjectileComponent, ProjectileHitEvent>(OnTrain);
        SubscribeLocalEvent<StaminaComponent, DamageChangedEvent>(OnHeadShot);
        SubscribeLocalEvent<SkillComponent, HitscanHitEvent>(TrainOnHitscanShoot);
    }
    private void TrainOnHitscanShoot(EntityUid uid, SkillComponent skillComp, ref HitscanHitEvent args)
    {
        if (args.Target == null || uid == args.Target)
            return;

        if (HasComp<ActorComponent>(args.Target) || HasComp<GunTrainerComponent>(args.Target))
        {
            _skillTrainerSystem.AddExperience(skillComp, skillType.RangeWeapon, (int)args.dmg.GetTotal());
        }
    }

    private void OnHeadShot(EntityUid uid, StaminaComponent component, DamageChangedEvent args)
    {
        if (!TryComp<SkillComponent>(args.Origin, out var skillcomp) || args.Origin == null || args.DamageDelta == null)
            return;

        float chance = skillcomp.RangeWeaponLevel switch
        {
            SkillLevel.None => 0.01f,
            SkillLevel.Basic => 0.1f,
            SkillLevel.Advanced => 0.2f,
            SkillLevel.Expert => 0.3f,
            _ => 0f
        };

        if (!_random.Prob(chance))
            return;

        var headshoter = args.Origin.Value;
        float staminadamage = args.DamageDelta.GetTotal().Float() * 3;

        _skillTrainerSystem.AddExperience(skillcomp, skillType.RangeWeapon, (int)staminadamage / 3);
        _audio.PlayPvs("/Audio/Vanilla/SkillSystem/headshot.ogg", uid, AudioParams.Default.WithVolume(-3f).WithMaxDistance(5f));

        _stamina.TakeStaminaDamage(uid, staminadamage, source: headshoter, ignoreResist: true);
    }
    private void OnTrain(EntityUid uid, ProjectileComponent component, ref ProjectileHitEvent args)
    {
        if (args.Shooter == null || args.Shooter == args.Target)
            return;

        if (HasComp<ActorComponent>(args.Target) || HasComp<GunTrainerComponent>(args.Target))
        {
            if (!TryComp<SkillComponent>(args.Shooter.Value, out var skillComp))
                skillComp = EnsureComp<SkillComponent>(args.Shooter.Value);

            _skillTrainerSystem.AddExperience(skillComp, skillType.RangeWeapon, (int)component.Damage.GetTotal());
        }
    }
    private void OnHandPickUp(EntityUid uid, GunComponent gunComp, GotEquippedHandEvent args)
    {
        if (HasComp<GunIgnoreSkillComponent>(uid))
            return;

        if (!EntityManager.TryGetComponent<UnskilledWeaponComponent>(uid, out var unskilledComp))
            unskilledComp = EnsureComp<UnskilledWeaponComponent>(uid);

        if (EntityManager.TryGetComponent<SkillComponent>(args.User, out var skillComp))
            UnskilledWeaponRefreshModifiers(skillComp, unskilledComp);
        else
        {
            unskilledComp.MinAnglePenalty = Angle.FromDegrees(50);
            unskilledComp.MaxAnglePenalty = Angle.FromDegrees(50);
        }
        _gun.RefreshModifiers(uid);
    }

    public void UnskilledWeaponRefreshModifiers(SkillComponent skillComp, UnskilledWeaponComponent unskilledComp)
    {
        switch (skillComp.RangeWeaponLevel)
        {
            case SkillLevel.None:
                unskilledComp.MinAnglePenalty = Angle.FromDegrees(50);
                unskilledComp.MaxAnglePenalty = Angle.FromDegrees(50);
                break;
            case SkillLevel.Basic:
                unskilledComp.MinAnglePenalty = Angle.FromDegrees(10);
                unskilledComp.MaxAnglePenalty = Angle.FromDegrees(10);
                break;
            case SkillLevel.Advanced:
            case SkillLevel.Expert:
                unskilledComp.MinAnglePenalty = Angle.FromDegrees(0);
                unskilledComp.MaxAnglePenalty = Angle.FromDegrees(0);
                break;
        }
    }


    private void OnGunRefreshModifiers(EntityUid uid, GunComponent gunComp, ref GunRefreshModifiersEvent args)
    {
        if (TryComp<UnskilledWeaponComponent>(uid, out var unskilledComp))
        {
            args.MinAngle += unskilledComp.MinAnglePenalty;
            args.MaxAngle += unskilledComp.MaxAnglePenalty;
            args.AngleIncrease += unskilledComp.AngleIncreasePenalty;
        }
    }

    private void RangeWeaponFalldownOnShoot(EntityUid uid, GunCanBeFallComponent component, GunShotEvent args)
    {
        if (!EntityManager.TryGetComponent<SkillComponent>(args.User, out var skillComp))
            skillComp = EnsureComp<SkillComponent>(args.User);

        if(HasComp<GunIgnoreSkillComponent>(uid))
            return;

        if (skillComp.RangeWeaponLevel < component.RequiresRangeWeaponLevel)
        {
            float FallChance = component.RequiresRangeWeaponLevel - skillComp.RangeWeaponLevel;

            FallChance = (FallChance > 0) ? FallChance * component.ChanceToFallPerLevel : 0;

            if (!_random.Prob(FallChance))
                return;


            // Получаем трансформацию пользователя
            var userTransform = EntityManager.GetComponent<TransformComponent>(args.User);

            // Определяем угол вращения
            var angle = userTransform.LocalRotation;

            // Расчет смещения
            var offset = angle.ToWorldVec() * -component.Recoil;

            // Получаем целевые координаты
            var targetCoordinates = userTransform.Coordinates.Offset(offset);

            // Вызываем метод выбрасывания
            _handsSystem.ThrowHeldItem(args.User, targetCoordinates);
            _audio.PlayPvs("/Audio/Weapons/Guns/Gunshots/bang.ogg", args.User, AudioParams.Default.WithMaxDistance(5f));
        }
    }

}
