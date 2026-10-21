using UnityEngine;

/// <summary>
/// Dungeon Authoring 데이터 검증에서 발견된 문제 한 건.
/// </summary>
public readonly struct DungeonDataValidationIssue
{
    public readonly string Message;
    public readonly Object Context;

    public DungeonDataValidationIssue(string message, Object context)
    {
        Message = message;
        Context = context;
    }
}
