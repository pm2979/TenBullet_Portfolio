using System.Collections.Generic;

/// <summary>
/// 결정적으로 완성된 Dungeon Enemy Spawn 배정 전체. Unity Scene이나 Pool을 바꾸지 않는다.
/// </summary>
public sealed class DungeonEnemyPlan
{
    public IReadOnlyList<DungeonEnemySpawnEntry> Entries { get; }

    internal DungeonEnemyPlan(IReadOnlyList<DungeonEnemySpawnEntry> entries)
    {
        Entries = entries;
    }
}
