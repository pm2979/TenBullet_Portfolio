using System;

/// <summary>
/// 조립된 Room 한 개의 계획과 View를 묶어 식별 정보만 제공한다. Room은 Combat 진행 상태의 권위가 아니며,
/// Run 완료 판정은 DungeonObjectiveTracker가 단독으로 가진다(dungeon_system-design.md 7.2).
/// </summary>
public sealed class RoomRuntime
{
    public DungeonRoomPlan Plan { get; }
    public RoomView View { get; }
    public string RoomId => Plan.RoomId;
    public RoomRole Role => Plan.Role;

    // 조립된 View와 계획을 결합한다.
    internal RoomRuntime(DungeonRoomPlan plan, RoomView view)
    {
        Plan = plan ?? throw new ArgumentNullException(nameof(plan));
        View = view ?? throw new ArgumentNullException(nameof(view));
    }
}
