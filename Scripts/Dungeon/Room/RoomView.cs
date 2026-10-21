using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Room Prefab의 진입 Trigger·출입 통제·Spawn 지점 참조를 제공하는 Unity View. Room 진행 상태는
/// 소유하지 않는다(dungeon_system-design.md 7.2).
/// </summary>
[DisallowMultipleComponent]
public sealed class RoomView : MonoBehaviour
{
    [Header("Room 참조")]
    [SerializeField] private Collider2D _entryTrigger;
    [SerializeField] private Tilemap[] _tilemaps;
    [SerializeField] private Collider2D[] _roomColliders;
    [SerializeField] private RoomSpawnPointView[] _spawnPoints;
    [SerializeField] private RoomAccessController _accessController;
    [Tooltip("1-Door Combat Room에만 연결한다. Boss Run에서 이 Room이 Portal 대상으로 선택될 때만 Runtime이 활성화한다.")]
    [SerializeField] private DungeonBossPortal _bossPortal;

    [Space(10)]
    [Header("진입 감지")]
    [Tooltip("DungeonSceneValidationGate가 Player 전용 Layer인지 확인한다. Teleport Room을 다시 도입할 때 " +
        "진입 판정에 재사용한다.")]
    [SerializeField] private LayerMask _entrantLayers = ~0;

    public string RoomId { get; private set; }
    public Collider2D EntryTrigger => _entryTrigger;
    public IReadOnlyList<Tilemap> Tilemaps => _tilemaps;
    public IReadOnlyList<Collider2D> RoomColliders => _roomColliders;
    public IReadOnlyList<RoomSpawnPointView> SpawnPoints => _spawnPoints;
    public RoomAccessController AccessController => _accessController;
    public DungeonBossPortal BossPortal => _bossPortal;
    public LayerMask EntrantLayers => _entrantLayers;

    // 역할에 맞는 Prefab 참조를 확인하고 Room ID와 초기 문 상태를 적용한다.
    // 실패 시 DungeonRuntimeBinder가 Room·Archetype 맥락과 함께 한 줄로 보고할 수 있도록 구체적 사유를 반환한다.
    internal bool TryInitialize(string roomId, RoomRole role, out string failureReason)
    {
        failureReason = BuildFailureReason(roomId, role);

        if (failureReason != null)
        {
            return false;
        }

        RoomId = roomId;
        return true;
    }

    // 실패 조건을 모두 모아 Prefab Inspector에서 바로 찾아 고칠 수 있는 구체적 위치·사유로 보고한다.
    // Boss Arena는 물리 Graph 밖의 별도 Stage라 Door 기반 진입(EntryTrigger·AccessController) 계약이
    // 적용되지 않는다(dungeon_system-design.md 4.1, 4.2a). 물리 Entrance/Combat/Corridor의 Door
    // 검증은 그대로 유지한다.
    private string BuildFailureReason(string roomId, RoomRole role)
    {
        List<string> reasons = new List<string>();
        bool requiresPhysicalDoorContract = role != RoomRole.Boss;

        if (string.IsNullOrWhiteSpace(roomId))
        {
            reasons.Add("roomId가 비어 있습니다.");
        }

        if (requiresPhysicalDoorContract)
        {
            if (_entryTrigger == null)
            {
                reasons.Add("EntryTrigger 참조가 비어 있습니다.");
            }
            else if (!_entryTrigger.isTrigger)
            {
                reasons.Add($"EntryTrigger({_entryTrigger.name})의 Is Trigger가 꺼져 있습니다.");
            }
        }

        string spatialReason = GetInvalidSpatialReferenceReason();

        if (spatialReason != null)
        {
            reasons.Add(spatialReason);
        }

        if (requiresPhysicalDoorContract)
        {
            if (_accessController == null)
            {
                reasons.Add("AccessController 참조가 비어 있습니다.");
            }
            else if (!_accessController.HasValidConfiguration())
            {
                reasons.Add("AccessController의 Door 목록이 비어 있거나 ConnectionId·Collider가 비어 있는 Door가 있습니다.");
            }
        }

        return reasons.Count == 0 ? null : string.Join(" / ", reasons);
    }

    // 안정 ID로 Encounter Spawn Transform을 찾는다.
    public bool TryGetSpawnPoint(string spawnPointId, out Transform spawnTransform)
    {
        if (!string.IsNullOrWhiteSpace(spawnPointId) && _spawnPoints != null)
        {
            for (int i = 0; i < _spawnPoints.Length; i++)
            {
                RoomSpawnPointView spawnPoint = _spawnPoints[i];

                if (spawnPoint != null &&
                    string.Equals(spawnPoint.SpawnPointId, spawnPointId, StringComparison.Ordinal))
                {
                    spawnTransform = spawnPoint.SpawnTransform;
                    return true;
                }
            }
        }

        spawnTransform = null;
        return false;
    }

    // Tilemap·공간 Collider·Spawn 지점 참조와 Spawn ID 중복을 확인하고, 실패한 정확한 위치를 반환한다.
    private string GetInvalidSpatialReferenceReason()
    {
        if (_tilemaps == null)
        {
            return "Tilemaps 배열이 비어 있습니다.";
        }

        if (_roomColliders == null || _roomColliders.Length == 0)
        {
            return "RoomColliders 배열이 비어 있습니다.";
        }

        if (_spawnPoints == null)
        {
            return "SpawnPoints 배열이 비어 있습니다.";
        }

        for (int i = 0; i < _tilemaps.Length; i++)
        {
            if (_tilemaps[i] == null)
            {
                return $"Tilemaps[{i}] 참조가 비어 있습니다.";
            }
        }

        for (int i = 0; i < _roomColliders.Length; i++)
        {
            if (_roomColliders[i] == null)
            {
                return $"RoomColliders[{i}] 참조가 비어 있습니다.";
            }
        }

        HashSet<string> spawnPointIds = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < _spawnPoints.Length; i++)
        {
            RoomSpawnPointView spawnPoint = _spawnPoints[i];

            if (spawnPoint == null)
            {
                return $"SpawnPoints[{i}] 참조가 비어 있습니다.";
            }

            if (string.IsNullOrWhiteSpace(spawnPoint.SpawnPointId))
            {
                return $"SpawnPoints[{i}]({spawnPoint.name})의 SpawnPointId가 비어 있습니다.";
            }

            if (!spawnPointIds.Add(spawnPoint.SpawnPointId))
            {
                return $"SpawnPoints[{i}]({spawnPoint.name})의 SpawnPointId '{spawnPoint.SpawnPointId}'가 중복됩니다.";
            }
        }

        return null;
    }
}
