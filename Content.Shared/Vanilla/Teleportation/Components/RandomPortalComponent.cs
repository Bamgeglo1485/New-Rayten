using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Content.Shared.Procedural;
using Content.Shared.Parallax.Biomes; 

namespace Content.Shared.Vanilla.Teleportation.Components;

/// <summary>
///     Телепортирует владельца в рандомное место на карте, либо создаёт отдельную карту с данжом
/// </summary>
[RegisterComponent]
public sealed partial class RandomPortalComponent : Component
{
    /// <summary>
    ///     Прототип выходного портала
    /// </summary>
    [DataField]
    public EntProtoId SecondPortalPrototype = "RandomPortalExit";

    /// <summary>
    ///     Телепортирует на любые тайлы на карте
    /// </summary>
    [DataField]
    public bool OnlyInMapTeleport = true;

    /// <summary>
    ///     Телепортирует только на тайлы станции
    /// </summary>
    [DataField]
    public bool OnlyInStationTeleport = false;

    /// <summary>
    ///     ДанжИ
    /// </summary>
    [DataField]
    public List<ProtoId> AllowedDungeons = new()
    {
        "Experiment",
        "SnowyLabs",
        "LavaBrig"
    };

    /// <summary>
    ///     Параллаксы данжов
    /// </summary>
    [DataField]
    public List<ProtoId> AllowedParallaxes = new()
    {
        "ExoStation",
        "PlasmaStation",
        "TrainStation",
        "Wizard",
        "AspidParallax",
        "CoreStation",
        "KettleStation",
        "Default"
    };

    /// <summary>
    ///     Биомы
    /// </summary>
    [DataField]
    public List<ProtoId> AllowedPlanets = new()
    {
        "PortalSnow",
        "PortalLava",
        "PortalGrasslands"
    };
}
