/// <summary>
/// WeakPointTarget의 현재 상태. 활성 여부와 노출 여부, 재생성 대기를 구분해 표현·직접 클릭 가능 여부를 결정한다
/// </summary>
public enum WeakPointState
{
    Inactive = 0,
    ActiveVisible = 1,
    /// <summary>활성이지만 플레이어가 볼 수 없는 상태(노출 Stat 꺼짐 또는 시야 거리 밖). 벽 차폐는 판정하지 않는다</summary>
    ActiveHidden = 2,
    Cooldown = 3
}
