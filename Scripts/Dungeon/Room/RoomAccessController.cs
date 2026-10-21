using System;
using UnityEngine;

/// <summary>
/// Room Door의 ConnectionId·Collider 식별만 제공한다. 모든 Door는 Gameplay 시작 전 확정된 실제 연결
/// 상태를 그대로 유지하며, Room 진행에 따른 Lock/Unlock이나 Navigation 부분 갱신을 수행하지 않는다
/// </summary>
public sealed class RoomAccessController : MonoBehaviour
{
    // RoomArchetypeSO._connections의 ConnectionId와 짝지어 어떤 물리 Door가 어떤 연결인지 식별한다.
    [Serializable]
    private sealed class Door
    {
        [Tooltip("RoomArchetypeSO._connections의 ConnectionId와 일치해야 한다.")]
        [SerializeField] private string _connectionId;
        [SerializeField] private Transform _anchor;

        public string ConnectionId => _connectionId;
        public Transform Anchor => _anchor;

        public bool IsValid => _anchor != null && !string.IsNullOrWhiteSpace(_connectionId);
    }

    [SerializeField] private Door[] _doors;

    // 지정 ConnectionId에 대응하는 물리 Door가 있는지 확인한다.
    public bool HasDoor(string connectionId)
    {
        if (_doors == null || string.IsNullOrWhiteSpace(connectionId))
        {
            return false;
        }

        for (int i = 0; i < _doors.Length; i++)
        {
            if (_doors[i] != null && string.Equals(_doors[i].ConnectionId, connectionId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    // 모든 문 참조가 런타임 적용에 유효한지 확인한다.
    public bool HasValidConfiguration()
    {
        if (_doors == null || _doors.Length == 0)
        {
            return false;
        }

        for (int i = 0; i < _doors.Length; i++)
        {
            if (_doors[i] == null || !_doors[i].IsValid)
            {
                return false;
            }
        }

        return true;
    }
}
