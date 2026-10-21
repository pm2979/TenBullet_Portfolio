using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Dungeon 계획과 조립이 공유하는 Room Prefab의 공간·역할 정적 정의.
/// </summary>
[CreateAssetMenu(fileName = "RoomArchetype_", menuName = "Scriptable Objects/Dungeon/Room Archetype")]
public sealed class RoomArchetypeSO : ScriptableObject
{
    [Space(5)]
    [Header("콘텐츠 식별")]
    [Tooltip("세이브·참조에 사용할 안정적인 콘텐츠 ID.")]
    [SerializeField] private string _contentId;
    [SerializeField] private GameObject _roomPrefab;

    [Space(10)]
    [Header("공간 계약")]
    [SerializeField] private RoomConnectionDefinition[] _connections;
    [SerializeField] private RoomSpawnPointDefinition[] _spawnPoints;

    [Space(10)]
    [Header("생성 규칙")]
    [Tooltip("이 Room이 담당할 수 있는 역할. 하나 이상 지정합니다.")]
    [SerializeField] private RoomRole[] _roles;
    [Min(1)]
    [SerializeField] private int _maximumUsesPerDungeon = 1;

    public string ContentId => _contentId;
    public GameObject RoomPrefab => _roomPrefab;
    public IReadOnlyList<RoomConnectionDefinition> Connections => _connections;
    public IReadOnlyList<RoomSpawnPointDefinition> SpawnPoints => _spawnPoints;
    public IReadOnlyList<RoomRole> Roles => _roles;
    public int MaximumUsesPerDungeon => _maximumUsesPerDungeon;

    // 이 Room이 지정된 역할을 담당할 수 있는지 반환한다.
    public bool HasRole(RoomRole role)
    {
        if (_roles == null)
        {
            return false;
        }

        for (int i = 0; i < _roles.Length; i++)
        {
            if (_roles[i] == role)
            {
                return true;
            }
        }

        return false;
    }
}
