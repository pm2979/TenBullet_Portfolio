using UnityEngine;

/// <summary>Enemy 정적 데이터에서 발견한 문제 메시지와 원본 객체를 전달</summary>
public readonly struct EnemyDataValidationIssue
{
    public readonly string Message;
    public readonly Object Context;

    public EnemyDataValidationIssue(string message, Object context)
    {
        Message = message;
        Context = context;
    }
}
