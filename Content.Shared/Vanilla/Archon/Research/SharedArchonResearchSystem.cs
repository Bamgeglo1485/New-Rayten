namespace Content.Shared.Vanilla.Archon.Research;

public abstract class SharedArchonResearchSystem : EntitySystem
{
    /// Выдаем очки за изучение архонта
    /// </summary>
    public abstract void ExtractResearchPoints(Entity<ArchonComponent> archon);
}
