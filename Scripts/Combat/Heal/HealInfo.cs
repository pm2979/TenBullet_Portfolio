/// <summary>
/// 절대값 기준의 체력 회복 요청을 전달하는 불변 값 타입이다.
/// </summary>
public readonly struct HealInfo
{
    public readonly int Amount;

    // 회복 구현체가 실제 적용량을 판정할 수 있도록 요청 회복량을 구성한다.
    public HealInfo(int amount)
    {
        Amount = amount;
    }
}
