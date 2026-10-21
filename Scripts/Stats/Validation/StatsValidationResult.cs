using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 능력치 데이터의 오류와 경고를 심각도별로 모아 호출자에게 제공한다.
/// </summary>
public sealed class StatsValidationResult
{
    private readonly List<StatsValidationIssue> _errors = new List<StatsValidationIssue>();
    private readonly List<StatsValidationIssue> _warnings = new List<StatsValidationIssue>();

    public IReadOnlyList<StatsValidationIssue> Errors => _errors;
    public IReadOnlyList<StatsValidationIssue> Warnings => _warnings;
    public bool HasErrors => _errors.Count > 0;

    // 데이터 사용을 막아야 하는 문제를 오류 목록에 추가한다.
    public void AddError(string message, Object context = null)
    {
        _errors.Add(new StatsValidationIssue(message, context));
    }

    // 사용할 수는 있지만 제작자가 확인해야 하는 문제를 경고 목록에 추가한다.
    public void AddWarning(string message, Object context = null)
    {
        _warnings.Add(new StatsValidationIssue(message, context));
    }
}
