using UnityEngine;

/// <summary>
/// Room Prefab 안의 Encounter Spawn 위치를 안정 ID로 노출한다.
/// </summary>
public sealed class RoomSpawnPointView : MonoBehaviour
{
    [Tooltip("RoomArchetypeSO의 Spawn 지점 ID와 같아야 합니다.")]
    [SerializeField] private string _spawnPointId;
    [Tooltip("RoomArchetypeSO의 같은 Spawn 지점 Kind와 같아야 합니다.")]
    [SerializeField] private DungeonSpawnPointKind _kind;

    public string SpawnPointId => _spawnPointId;
    public DungeonSpawnPointKind Kind => _kind;
    public Transform SpawnTransform => transform;
}
