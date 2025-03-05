using Robust.Shared.GameStates;
using Content.Shared.Vanilla.Skill;
using Content.Server.SkillTrainer;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Player;
using Robust.Shared.Audio.Systems;

namespace Content.Server.Vanilla.Skill;

public sealed class EmitSkillExperienceOnSpawnSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly ServerSkillTrainerSystem _skillTrainerSystem = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<EmitSkillExperienceOnSpawnComponent, MapInitEvent>(OnEntitySpawned);
    }

    private void OnEntitySpawned(EntityUid uid, EmitSkillExperienceOnSpawnComponent component, MapInitEvent args)
    {
        // Получаем координаты спавна сущности
        var spawnCoords = Transform(uid).Coordinates;

        // Находим всех игроков в радиусе
        foreach (var entity in _lookup.GetEntitiesInRange(spawnCoords, component.Radius))
        {

            if (!TryComp<SkillComponent>(entity, out var SkillComp))
                continue;

            TryComp<ActorComponent>(entity, out var actor);

            _skillTrainerSystem.AddExperience(SkillComp, component.SkillType, component.ExperienceAmount, player: actor?.PlayerSession);
        }
    }
}