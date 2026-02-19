using Content.Shared.Vanilla.Archon.OldMan;
using Content.Shared.Actions;
using Content.Shared.GridPreloader.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Audio;
using Robust.Shared.Utility;
using Robust.Shared.Map;
namespace Content.Shared.Vanilla.Archon.OldMan;

[RegisterComponent]
public sealed partial class OldManComponent : Component
{
    [ViewVariables]
    public EntityUid? PolyMorphEntity;

    #region звуки и анимации
    /// <summary>
    /// звук ухода и появления в карманное измерение
    /// </summary>
    [DataField]
    public SoundSpecifier TeleportSound = new SoundCollectionSpecifier("106teleport");
    [DataField]
    public SoundSpecifier MapInitSound = new SoundPathSpecifier("/Audio/Vanilla/Effects/Archon/106/106mapinit.ogg");
    /// <summary>
    /// Координаты ухода в карманное измерение, на них старик телепортируется при невалидной точке выхода (космос, другой грид)
    /// </summary>
    public EntityCoordinates? FallBackCoords = null;
    /// <summary>
    /// Грид ухода в карманное измерение, телепортация разрешена только в пределах одного грида
    /// </summary>
    public EntityUid PreviousGrid = default;
    /// <summary>
    /// Длительность входа в портал
    /// </summary>
    [DataField]
    public TimeSpan TeleportInDuration = TimeSpan.FromSeconds(2.45);
    /// <summary>
    /// Длительность выхода из портала
    /// </summary>
    [DataField]
    public TimeSpan TeleportOutDuration = TimeSpan.FromSeconds(2.6);
    /// <summary>
    /// путь к карманному измерению
    /// </summary>
    [DataField]
    public ResPath DimensionMap = new ResPath("/Maps/Vanilla/Misc/PocketDimension.yml");
    /// <summary>
    /// предзагруженное карманное измерение
    /// </summary>
    [DataField(customTypeSerializer: typeof(PrototypeIdSerializer<PreloadedGridPrototype>))]
    public string PreLoadGridProto = "106Dimension";

    [DataField("actionTeleport", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string ActionId = "Action106Teleport";
    public EntityUid? ActionEnt;
    #endregion
    /// <summary>
    /// Грид карманного измерения, на него возвращается старик
    /// </summary>
    public EntityUid DimensionGridUid = default;
    /// <summary>
    /// карта карманного измерения
    /// </summary>
    public EntityUid DimensionUid = default;
    /// <summary>
    /// Грид станции
    /// </summary>
    public EntityUid StationGridUid = default;
    /// <summary>
    /// дедушка телепортируется?
    /// </summary>
    public TeleportState TPState = TeleportState.NoTP;
    /// <summary>
    /// момент времени когда анимация захода в портал закончится
    /// </summary>
    public TimeSpan TeleportationInEndAt = TimeSpan.Zero;
    /// <summary>
    /// момент времени когда анимация выхода из портала закончится
    /// </summary>
    public TimeSpan TeleportationOutEndAt = TimeSpan.Zero;
}
[RegisterComponent]
public sealed partial class OldManPolymorphComponent : Component
{

}
[Serializable, NetSerializable]
public enum OldManVisuals : byte
{
    teleport,
}

[Serializable, NetSerializable]
public enum TeleportState : byte
{
    In,//входим
    Out,//выходим
    NoTP//не в телепортации
}

public sealed partial class OldManTeleportEvent : InstantActionEvent { }
