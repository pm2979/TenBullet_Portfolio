/// <summary>
/// DungeonEnemyDeployment이 보유하는 계획 Entry와 실제 생성된 EnemyActor의 대응 한 건.
/// </summary>
public sealed class DungeonEnemyDeploymentEntry
{
    public DungeonEnemySpawnEntry PlanEntry { get; }
    public EnemyActor Actor { get; }

    internal DungeonEnemyDeploymentEntry(DungeonEnemySpawnEntry planEntry, EnemyActor actor)
    {
        PlanEntry = planEntry;
        Actor = actor;
    }
}
