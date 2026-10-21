/// <summary>Enemy 대여 성공 Actor 또는 식별 가능한 실패 이유를 전달</summary>
public readonly struct EnemyRentResult
{
    public readonly bool IsSuccess;
    public readonly EnemyActor Actor;
    public readonly EnemyRentFailureReason FailureReason;

    private EnemyRentResult(bool isSuccess, EnemyActor actor, EnemyRentFailureReason failureReason)
    {
        IsSuccess = isSuccess;
        Actor = actor;
        FailureReason = failureReason;
    }

    /// <summary>대여된 Actor를 포함하는 성공 결과 생성</summary>
    public static EnemyRentResult Success(EnemyActor actor)
    {
        return new EnemyRentResult(true, actor, EnemyRentFailureReason.None);
    }

    /// <summary>Actor 없이 실패 이유만 포함하는 결과 생성</summary>
    public static EnemyRentResult Failure(EnemyRentFailureReason reason)
    {
        return new EnemyRentResult(false, null, reason);
    }
}
