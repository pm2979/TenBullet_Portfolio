/// <summary>
/// Dungeon Bootstrap 준비 성공 또는 실패 단계와 문맥을 전달한다.
/// </summary>
public readonly struct DungeonBootstrapResult
{
    public bool Succeeded { get; }
    public DungeonBootstrapFailure Failure { get; }
    public string Message { get; }

    // Gameplay 공개까지 완료된 결과를 만든다.
    public static DungeonBootstrapResult Success()
    {
        return new DungeonBootstrapResult(true, DungeonBootstrapFailure.None, string.Empty);
    }

    // 부분 Runtime을 노출하지 않는 실패 결과를 만든다.
    public static DungeonBootstrapResult Failed(DungeonBootstrapFailure failure, string message)
    {
        return new DungeonBootstrapResult(false, failure, message);
    }

    private DungeonBootstrapResult(bool succeeded, DungeonBootstrapFailure failure, string message)
    {
        Succeeded = succeeded;
        Failure = failure;
        Message = message;
    }
}
