using System.Collections.Generic;
using System.Collections.ObjectModel;

/// <summary>
/// DungeonBuildPlan의 생성 규칙 위반 목록.
/// </summary>
public sealed class DungeonPlanValidationResult
{
    private readonly List<string> _errors = new List<string>();
    private ReadOnlyCollection<string> _readOnlyErrors;

    public bool IsValid => _errors.Count == 0;
    public IReadOnlyList<string> Errors => _readOnlyErrors ??=
        new ReadOnlyCollection<string>(_errors);

    // 계획 확정을 막는 검증 오류를 추가한다.
    internal void AddError(string error)
    {
        _errors.Add(error);
    }
}
