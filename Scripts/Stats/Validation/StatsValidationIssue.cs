using UnityEngine;

/// <summary>
/// 능력치 데이터 검증에서 발견한 문제와 관련 에셋을 함께 보관한다.
/// </summary>
public readonly struct StatsValidationIssue
{
    public readonly string Message;
    public readonly Object Context;

    public StatsValidationIssue(string message, Object context)
    {
        Message = message;
        Context = context;
    }
}
