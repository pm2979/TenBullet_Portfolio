/// <summary>
/// 절대값 회복 요청의 실제 적용 결과를 전달하는 불변 값 타입이다.
/// </summary>
public readonly struct HealResult
{
    public readonly bool WasApplied;
    public readonly int AppliedAmount;

    private HealResult(bool wasApplied, int appliedAmount)
    {
        WasApplied = wasApplied;
        AppliedAmount = appliedAmount;
    }

    public static readonly HealResult NotApplied = new HealResult(false, 0);

    // 최대 체력 제한 등을 반영해 실제로 회복된 양을 결과로 만든다.
    public static HealResult Applied(int appliedAmount)
    {
        return new HealResult(true, appliedAmount);
    }
}
