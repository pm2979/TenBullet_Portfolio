using System;
using System.Collections.Generic;

/// <summary>
/// 목표 완료와 Dungeon 종료 사실의 단일 런타임 권위. Room 진행 상태는 다루지 않는다
/// (dungeon_system-design.md 7.2).
/// </summary>
public sealed class DungeonSession : IDisposable
{
    // Boss Arena가 같은 Run에 두 번 등록되는 것을 막기 위해 등록된 Room ID만 보관한다.
    private readonly HashSet<string> _roomIds = new HashSet<string>(StringComparer.Ordinal);
    private DungeonObjectiveTracker _objectiveTracker;
    private bool _disposed;

    public event Action ObjectiveCompleted;
    public event Action<DungeonRunResult> Ended;

    public bool HasEnded { get; private set; }
    public DungeonRunResult RunResult { get; private set; }

    // Tracker가 목표 완료를 보고했는지 그대로 반환한다(Room Role·Room Clear 수·Exit Room을 쓰지 않는다).
    public bool IsObjectiveCompleted => _objectiveTracker != null && _objectiveTracker.IsCompleted;

    // 목표 완료 뒤 아직 종료되지 않은 동안만 탈출을 허용한다.
    public bool CanEscape => IsObjectiveCompleted && !HasEnded;

    // 이번 Build에 조립된 Room ID를 등록한다.
    internal DungeonSession(IReadOnlyList<RoomRuntime> rooms)
    {
        if (rooms == null)
        {
            throw new ArgumentNullException(nameof(rooms));
        }

        for (int i = 0; i < rooms.Count; i++)
        {
            RoomRuntime room = rooms[i] ?? throw new ArgumentException("RoomRuntime이 null입니다.", nameof(rooms));
            _roomIds.Add(room.RoomId);
        }
    }

    // Tracker를 연결해 목표 완료 상태를 읽고 완료 이벤트를 중계한다. Build당 한 번만 호출한다.
    internal void AttachObjectiveTracker(DungeonObjectiveTracker tracker)
    {
        _objectiveTracker = tracker ?? throw new ArgumentNullException(nameof(tracker));
        _objectiveTracker.Completed += HandleObjectiveCompleted;
    }

    // Boss Portal 전환 성공 뒤 Arena Room을 이번 Run의 Room으로 등록한다. 물리 Room과 달리 생성자 시점에
    // 등록되지 않으며, 전환은 단방향이라 같은 Run에서 두 번 등록되면 거부한다(dungeon_system-design.md 4.2a).
    internal bool EnterArenaRoom(RoomRuntime arenaRoom)
    {
        if (_disposed || HasEnded || arenaRoom == null)
        {
            return false;
        }

        return _roomIds.Add(arenaRoom.RoomId);
    }

    // 목표 완료 전이거나 이미 종료된 뒤에는 거부한다. 성공하면 Cleared를 정확히 한 번 확정한다.
    public bool TryEscape()
    {
        if (_disposed || HasEnded || !IsObjectiveCompleted)
        {
            return false;
        }

        return TryEnd(DungeonRunResult.Cleared);
    }

    // 클리어 또는 실패 결과를 한 번만 확정해 알린다.
    public bool TryEnd(DungeonRunResult result)
    {
        if (_disposed || HasEnded)
        {
            return false;
        }

        HasEnded = true;
        RunResult = result;
        Ended?.Invoke(result);
        return true;
    }

    // Tracker 구독과 Session 이벤트 참조를 모두 제거한다.
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // 남은 Actor를 돌려주는 DungeonAssembly.Dispose()보다 먼저 Tracker 구독을 해제한다.
        if (_objectiveTracker != null)
        {
            _objectiveTracker.Completed -= HandleObjectiveCompleted;
            _objectiveTracker.Dispose();
            _objectiveTracker = null;
        }

        _roomIds.Clear();
        ObjectiveCompleted = null;
        Ended = null;
    }

    // Tracker의 완료 신호를 Session 구독자(UI 등)에게 그대로 중계한다.
    private void HandleObjectiveCompleted()
    {
        ObjectiveCompleted?.Invoke();
    }
}
