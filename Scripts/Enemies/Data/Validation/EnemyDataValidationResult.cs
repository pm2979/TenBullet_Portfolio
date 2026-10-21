using System.Collections.Generic;
using UnityEngine;

/// <summary>Enemy 정적 데이터의 오류와 경고를 분리해 누적하는 검증 결과</summary>
public sealed class EnemyDataValidationResult
{
    private readonly List<EnemyDataValidationIssue> _errors = new List<EnemyDataValidationIssue>();
    private readonly List<EnemyDataValidationIssue> _warnings = new List<EnemyDataValidationIssue>();

    public IReadOnlyList<EnemyDataValidationIssue> Errors => _errors;
    public IReadOnlyList<EnemyDataValidationIssue> Warnings => _warnings;
    public bool HasErrors => _errors.Count > 0;

    /// <summary>실행을 막아야 하는 데이터 오류 추가</summary>
    public void AddError(string message, Object context = null)
    {
        _errors.Add(new EnemyDataValidationIssue(message, context));
    }

    /// <summary>실행은 허용하지만 수정이 필요한 데이터 경고 추가</summary>
    public void AddWarning(string message, Object context = null)
    {
        _warnings.Add(new EnemyDataValidationIssue(message, context));
    }
}
