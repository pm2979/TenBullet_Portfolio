using UnityEngine;

/// <summary>
/// 피해 요청 단위로 소비되는 Shield Stack만 소유하는 컴포넌트.
/// HP·지속시간·재생성·Stat은 알지 못하며, 명시적 초기화와 소비만 담당한다
/// </summary>
public sealed class Shield : MonoBehaviour
{
    public int StackCount { get; private set; }

    public bool IsActive => StackCount > 0;

    /// <summary>
    /// Stack 수를 설정한다. 소유자 초기화 지점에서 명시적으로 호출한다
    /// </summary>
    public void SetStackCount(int stackCount)
    {
        StackCount = Mathf.Max(0, stackCount);
    }

    /// <summary>
    /// 유효한 구조화 피해 요청 1건을 차단할 때 Stack 1개를 소비한다.
    /// 호출 전 IsActive로 소비 가능 여부를 확인해야 한다
    /// </summary>
    public void ConsumeStack()
    {
        if (StackCount <= 0)
            return;

        StackCount--;
    }

    /// <summary>
    /// Pool 반환 등 재사용 경계에서 Stack을 0으로 되돌린다. 다음 대여는 SetStackCount를 다시 명시적으로 호출해야 한다
    /// </summary>
    public void ResetRuntimeState()
    {
        StackCount = 0;
    }
}
