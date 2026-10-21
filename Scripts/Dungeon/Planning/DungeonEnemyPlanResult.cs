/// <summary>
/// DungeonEnemyPlanner의 성공 계획 또는 구조화된 실패를 전달한다.
/// </summary>
public sealed class DungeonEnemyPlanResult
{
    public bool Succeeded { get; }
    public DungeonEnemyPlan Plan { get; }
    public DungeonEnemyPlanFailure Failure { get; }
    public string Message { get; }

    // 완성된 계획만 노출하는 성공 결과를 만든다.
    internal static DungeonEnemyPlanResult Success(DungeonEnemyPlan plan)
    {
        return new DungeonEnemyPlanResult(true, plan, DungeonEnemyPlanFailure.None, string.Empty);
    }

    // 계획을 노출하지 않는 실패 결과를 만든다.
    internal static DungeonEnemyPlanResult Failed(DungeonEnemyPlanFailure failure, string message)
    {
        return new DungeonEnemyPlanResult(false, null, failure, message);
    }

    private DungeonEnemyPlanResult(
        bool succeeded, DungeonEnemyPlan plan, DungeonEnemyPlanFailure failure, string message)
    {
        Succeeded = succeeded;
        Plan = plan;
        Failure = failure;
        Message = message;
    }
}
