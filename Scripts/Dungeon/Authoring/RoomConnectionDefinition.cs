using System;
using UnityEngine;

/// <summary>
/// Room 배치 단계에서 서로 맞물릴 수 있는 출입구 하나의 정적 계약.
/// </summary>
[Serializable]
public sealed class RoomConnectionDefinition
{
    /// <summary>
    /// 현재 유일하게 지원하는 연결 규격의 ID. 다른 타입이 실제로 필요해질 때 Edgar 후보 호환 규칙과
    /// 함께 추가한다.
    /// </summary>
    public const string StandardConnectionTypeId = "standard";

    /// <summary>
    /// Standard 연결 규격의 통로 폭(Tile 수).
    /// </summary>
    public const int StandardPassageWidthInCells = 3;

    [Tooltip("Room 안에서 출입구를 식별하는 안정적인 ID.")]
    [SerializeField] private string _connectionId;
    [Tooltip("Door Line의 최소 좌표 Cell. Left/Right는 +Y, Up/Down은 +X 방향으로 폭만큼 확장된다.")]
    [SerializeField] private Vector2Int _cell;
    [SerializeField] private RoomConnectionDirection _direction;
    [Tooltip("같은 타입 ID끼리만 연결되는 출입구 계약. 현재는 standard만 지원한다.")]
    [SerializeField] private string _connectionTypeId;
    [Min(1)]
    [SerializeField] private int _passageWidthInCells = 1;

    public string ConnectionId => _connectionId;
    public Vector2Int Cell => _cell;
    public RoomConnectionDirection Direction => _direction;
    public string ConnectionTypeId => _connectionTypeId;
    public int PassageWidthInCells => _passageWidthInCells;

    /// <summary>
    /// Cell+Direction+PassageWidthInCells 규칙으로 계산한 Door Line의 반대쪽 끝 Cell을 반환한다.
    /// Left/Right는 +Y, Up/Down은 +X 방향으로 (폭-1)만큼 확장한다.
    /// </summary>
    public Vector2Int GetDoorLineEndCell()
    {
        bool extendsAlongY = _direction == RoomConnectionDirection.Left || _direction == RoomConnectionDirection.Right;
        int extent = Mathf.Max(0, _passageWidthInCells - 1);

        return extendsAlongY
            ? new Vector2Int(_cell.x, _cell.y + extent)
            : new Vector2Int(_cell.x + extent, _cell.y);
    }
}
