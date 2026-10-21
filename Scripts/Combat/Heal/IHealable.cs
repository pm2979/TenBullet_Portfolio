/// <summary>
/// 절대값 기준의 체력 회복 요청을 처리하는 대상의 공통 계약이다.
/// </summary>
public interface IHealable
{
    /// <summary>
    /// 절대값 회복을 시도하고 실제 적용량을 반환한다.
    /// </summary>
    HealResult TryRestoreHealth(HealInfo healInfo);
}
