using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Dungeon Authoring 데이터의 오류와 경고를 호출자가 구분해 확인할 수 있는 검증 결과.
/// </summary>
public sealed class DungeonDataValidationResult
{
    private readonly List<DungeonDataValidationIssue> _errors = new List<DungeonDataValidationIssue>();
    private readonly List<DungeonDataValidationIssue> _warnings = new List<DungeonDataValidationIssue>();

    public IReadOnlyList<DungeonDataValidationIssue> Errors => _errors;
    public IReadOnlyList<DungeonDataValidationIssue> Warnings => _warnings;
    public bool HasErrors => _errors.Count > 0;

    // 검증 실패로 처리할 오류를 추가한다.
    public void AddError(string message, Object context = null)
    {
        _errors.Add(new DungeonDataValidationIssue(message, context));
    }

    // 실행을 막지는 않지만 제작자가 확인해야 할 경고를 추가한다.
    public void AddWarning(string message, Object context = null)
    {
        _warnings.Add(new DungeonDataValidationIssue(message, context));
    }
}
