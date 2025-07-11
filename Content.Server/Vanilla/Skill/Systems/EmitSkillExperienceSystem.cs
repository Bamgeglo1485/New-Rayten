using Robust.Shared.GameStates;
using Content.Shared.Vanilla.Skill;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Player;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Timing;
using Content.Shared.Anomaly.Components;
using Content.Shared.Xenoarchaeology.Artifact.Components;
using Content.Shared.Xenoarchaeology.Artifact;

namespace Content.Server.Vanilla.Skill;

public sealed class EmitSkillExperienceSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedSkillTrainerSystem _skillTrainerSystem = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    private TimeSpan _nextTick = TimeSpan.Zero;
    private const float Interval = 1.0f; // 1 секунда

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<EmitSkillExperienceOnSpawnComponent, MapInitEvent>(OnEntitySpawned);
        SubscribeLocalEvent<XenoArtifactComponent, XenoArtifactActivatedEvent>(OnArtifactActivated);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_gameTiming.CurTime < _nextTick)
            return;

        _nextTick = _gameTiming.CurTime + TimeSpan.FromSeconds(Interval);

        foreach (var anomaly in EntityQuery<AnomalyComponent>())
        {
            var coords = Transform(anomaly.Owner).Coordinates;
            EmitSkillExperience(coords, 5.0f, 1, skillType.Research);
        }
    }
    private void OnArtifactActivated(EntityUid uid, XenoArtifactComponent component, XenoArtifactActivatedEvent args)
    {
        // Получаем координаты спавна сущности
        var Coords = Transform(uid).Coordinates;
        EmitSkillExperience(Coords, 5.0f, 10, skillType.Research);
    }


    private void OnEntitySpawned(EntityUid uid, EmitSkillExperienceOnSpawnComponent component, MapInitEvent args)
    {
        // Получаем координаты спавна сущности
        var spawnCoords = Transform(uid).Coordinates;
        EmitSkillExperience(spawnCoords, component.Radius, component.ExperienceAmount, component.SkillType);
    }

    public void EmitSkillExperience(EntityCoordinates coords, float Radius, int ExperienceAmount, skillType SkillType)
    {
        foreach (var entity in _lookup.GetEntitiesInRange(coords, Radius))
        {

            if (!TryComp<SkillComponent>(entity, out var SkillComp))
                continue;

            _skillTrainerSystem.AddExperience(SkillComp, SkillType, ExperienceAmount);
        }
    }
}
