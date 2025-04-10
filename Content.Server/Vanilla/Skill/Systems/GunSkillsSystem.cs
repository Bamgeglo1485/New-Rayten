using System.Numerics;
using Content.Shared.Hands;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Components;
using Content.Shared.Item;
using Content.Shared.Damage;
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

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GunComponent, GotEquippedHandEvent>(OnHandPickUp);//обновляет модификаторы при взятии оружия в руки
        SubscribeLocalEvent<GunComponent, GunRefreshModifiersEvent>(OnGunRefreshModifiers);//перегрузка обновления модификаторов
        SubscribeLocalEvent<GunCanBeFallComponent, GunShotEvent>(RangeWeaponFalldownOnShoot);//выпадение оружия при стрельбе
        SubscribeLocalEvent<ProjectileComponent, ProjectileHitEvent>(TrainOnShoot);
        SubscribeLocalEvent<SkillComponent, HitscanHitEvent>(TrainOnHitscanShoot);
    }

    private void TrainOnHitscanShoot(EntityUid uid, SkillComponent skillComp, ref HitscanHitEvent args)
    {
        if(args.Target == null || uid == args.Target)
            return;

        if(HasComp<ActorComponent>(args.Target) || HasComp<GunTrainerComponent>(args.Target))
        {
            if(_skillTrainerSystem.AddExperience(skillComp, skillType.RangeWeapon, (int)args.dmg.GetTotal()))
            {
                if(TryComp<UnskilledWeaponComponent>(args.SourceItem, out var unskilledComp))
                {
                    UnskilledWeaponRefreshModifiers(skillComp, unskilledComp);
                    _gun.RefreshModifiers(args.SourceItem);
                }
            }

        }
    }
    private void TrainOnShoot(EntityUid uid, ProjectileComponent component, ref ProjectileHitEvent args)
    {
        if(args.Shooter == null || args.Shooter == args.Target)
            return;

        if(HasComp<ActorComponent>(args.Target) || HasComp<GunTrainerComponent>(args.Target))
        {

            if (!EntityManager.TryGetComponent<SkillComponent>(args.Shooter.Value, out var skillComp))
                skillComp = EnsureComp<SkillComponent>(args.Shooter.Value);

            if(_skillTrainerSystem.AddExperience(skillComp, skillType.RangeWeapon, (int)component.Damage.GetTotal()))
            {
                if(component.Weapon == null)
                    return;

                if(TryComp<UnskilledWeaponComponent>(component.Weapon.Value, out var unskilledComp))
                {
                    UnskilledWeaponRefreshModifiers(skillComp, unskilledComp);
                    _gun.RefreshModifiers(component.Weapon.Value);
                }
            }

        }
    }

    private void OnHandPickUp(EntityUid uid, GunComponent gunComp, GotEquippedHandEvent args)
    {
        if(HasComp<GunIgnoreSkillComponent>(uid))
            return;

        if (!EntityManager.TryGetComponent<UnskilledWeaponComponent>(uid, out var unskilledComp))
            unskilledComp = EnsureComp<UnskilledWeaponComponent>(uid);

        if (EntityManager.TryGetComponent<SkillComponent>(args.User, out var skillComp))
            UnskilledWeaponRefreshModifiers(skillComp, unskilledComp);
        else
        {
            unskilledComp.MinAnglePenalty = Angle.FromDegrees(60);
            unskilledComp.MaxAnglePenalty = Angle.FromDegrees(150);
            unskilledComp.AngleIncreasePenalty = Angle.FromDegrees(18);
        }
        _gun.RefreshModifiers(uid);
    }

    private void UnskilledWeaponRefreshModifiers(SkillComponent skillComp, UnskilledWeaponComponent unskilledComp)
    {
        switch (skillComp.RangeWeaponLevel)
        {
            case SkillLevel.None:
                unskilledComp.MinAnglePenalty = Angle.FromDegrees(60);
                unskilledComp.MaxAnglePenalty = Angle.FromDegrees(150);
                unskilledComp.AngleIncreasePenalty = Angle.FromDegrees(15);
                break;
            case SkillLevel.Basic:
                unskilledComp.MinAnglePenalty = Angle.FromDegrees(0);
                unskilledComp.MaxAnglePenalty = Angle.FromDegrees(100);
                unskilledComp.AngleIncreasePenalty = Angle.FromDegrees(10);
                break;
            case SkillLevel.Advanced:
                unskilledComp.MinAnglePenalty = Angle.FromDegrees(0);
                unskilledComp.MaxAnglePenalty = Angle.FromDegrees(50);
                unskilledComp.AngleIncreasePenalty = Angle.FromDegrees(5);
                break;
            case SkillLevel.Expert:
                unskilledComp.MinAnglePenalty = Angle.FromDegrees(0);
                unskilledComp.MaxAnglePenalty = Angle.FromDegrees(0);
                unskilledComp.AngleIncreasePenalty = Angle.FromDegrees(0);
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
