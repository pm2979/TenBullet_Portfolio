using System;

/// <summary>
/// DungeonEnemyPlan이 소유하는 Spawn 배정 한 건. 런타임 Transform·EnemyActor 참조를 포함하지 않는다.
/// </summary>
public sealed class DungeonEnemySpawnEntry
{
    public string RoomId { get; }
    public string SpawnPointId { get; }
    public DungeonSpawnPointKind Kind { get; }
    public EnemyArchetypeSO Archetype { get; }

    public DungeonEnemySpawnEntry(
        string roomId, string spawnPointId, DungeonSpawnPointKind kind, EnemyArchetypeSO archetype)
    {
        if (string.IsNullOrWhiteSpace(roomId))
        {
            throw new ArgumentException("roomId가 비어 있습니다.", nameof(roomId));
        }

        if (string.IsNullOrWhiteSpace(spawnPointId))
        {
            throw new ArgumentException("spawnPointId가 비어 있습니다.", nameof(spawnPointId));
        }

        if (archetype == null)
        {
            throw new ArgumentNullException(nameof(archetype));
        }

        RoomId = roomId;
        SpawnPointId = spawnPointId;
        Kind = kind;
        Archetype = archetype;
    }
}
