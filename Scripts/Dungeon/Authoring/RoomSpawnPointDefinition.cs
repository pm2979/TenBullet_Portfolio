using System;
using UnityEngine;

/// <summary>
/// Room 내부에서 Enemy Spawn 계획이 사용할 Spawn 지점의 안정 식별자.
/// </summary>
/// <remarks>
/// 실제 위치는 Room Prefab의 RoomSpawnPointView Transform이 전담하고, 이 정의는
/// DungeonEnemyPlanner가 SpawnPointId 문자열 매칭에만 사용한다.
/// </remarks>
[Serializable]
public sealed class RoomSpawnPointDefinition
{
    [Tooltip("Room 안에서 Spawn 지점을 식별하는 안정적인 ID.")]
    [SerializeField] private string _spawnPointId;
    [Tooltip("이 Spawn 지점이 수용하는 배치 대상 분류.")]
    [SerializeField] private DungeonSpawnPointKind _kind;

    public string SpawnPointId => _spawnPointId;
    public DungeonSpawnPointKind Kind => _kind;
}
