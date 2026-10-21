/// <summary>
/// 완전한 Dungeon 조립 결과 또는 구조화된 실패를 전달한다.
/// </summary>
public sealed class DungeonAssemblyResult
{
    public bool Succeeded { get; }
    public DungeonAssembly Assembly { get; }
    public DungeonAssemblyFailure Failure { get; }
    public string Message { get; }

    // 모든 Room이 조립된 성공 결과를 만든다.
    internal static DungeonAssemblyResult Success(DungeonAssembly assembly)
    {
        return new DungeonAssemblyResult(true, assembly, DungeonAssemblyFailure.None, string.Empty);
    }

    // 조립 객체를 노출하지 않는 실패 결과를 만든다.
    internal static DungeonAssemblyResult Failed(DungeonAssemblyFailure failure, string message)
    {
        return new DungeonAssemblyResult(false, null, failure, message);
    }

    // 결과 상태와 선택적 조립 객체를 보관한다.
    private DungeonAssemblyResult(
        bool succeeded,
        DungeonAssembly assembly,
        DungeonAssemblyFailure failure,
        string message)
    {
        Succeeded = succeeded;
        Assembly = assembly;
        Failure = failure;
        Message = message;
    }
}
