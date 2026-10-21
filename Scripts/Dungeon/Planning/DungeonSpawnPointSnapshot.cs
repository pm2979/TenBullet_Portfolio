using System;

/// <summary>
/// 조립된 Room의 Spawn Point 하나를 MonoBehaviour·Scene 상태 없이 DungeonEnemyPlanner에 전달하는 불변 스냅샷.
/// </summary>
public sealed class DungeonSpawnPointSnapshot
{
    public string RoomId { get; }
    public string SpawnPointId { get; }
    public DungeonSpawnPointKind Kind { get; }

    public DungeonSpawnPointSnapshot(string roomId, string spawnPointId, DungeonSpawnPointKind kind)
    {
        if (string.IsNullOrWhiteSpace(roomId))
        {
            throw new ArgumentException("roomId가 비어 있습니다.", nameof(roomId));
        }

        if (string.IsNullOrWhiteSpace(spawnPointId))
        {
            throw new ArgumentException("spawnPointId가 비어 있습니다.", nameof(spawnPointId));
        }

        RoomId = roomId;
        SpawnPointId = spawnPointId;
        Kind = kind;
    }
}
