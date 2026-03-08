namespace Content.Shared.Vanilla.Voices;

[RegisterComponent]
public sealed partial class PrivateTalkComponent : Component
{
    /// <summary>
    /// Сущности которые будут слышать любое чат-сообщение,
    /// </summary>
    [DataField]
    public HashSet<EntityUid> Receivers = [];
}
