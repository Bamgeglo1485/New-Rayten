using Robust.Shared.Serialization;

namespace Content.Shared.Vanilla.Competitive;

[RegisterComponent]
public sealed partial class CompetitiveComponent : Component
{
    [DataField]
    public CompetitiveDifficult Difficult = CompetitiveDifficult.easy;
    [DataField]
    public string ActualName = "Неизвестно";
    [DataField]
    public LocId HiddenDesc = string.Empty;

    [DataField]
    public bool EnemyTechnology = false;
}

[Serializable, NetSerializable]
public enum CompetitiveDifficult
{
    easy = 1, // 4 символа
    medium = 2, // 6 символов
    hard = 3, // 6 символов, без сброса
}
