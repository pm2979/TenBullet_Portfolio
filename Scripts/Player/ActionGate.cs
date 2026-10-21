/// <summary>
/// 중첩된 행동 차단 요청을 집계해 현재 행동 가능 여부를 제공한다.
/// </summary>
public sealed class ActionGate
{
    private int _blockCount;

    public bool IsOpen => _blockCount <= 0;

    /// <summary>
    /// 새 차단 요청을 추가한다.
    /// </summary>
    public void Block()
    {
        _blockCount++;
    }

    /// <summary>
    /// 차단 요청 하나를 해제하며, 짝이 없는 해제 요청은 무시한다.
    /// </summary>
    public void Unblock()
    {
        if (_blockCount > 0) _blockCount--;
    }
}
