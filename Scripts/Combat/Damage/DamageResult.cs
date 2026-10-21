/// <summary>
/// Health가 판정한 피해 적용 결과를 전달하는 불변 값 타입이다.
/// </summary>
public readonly struct DamageResult
{
    public readonly bool WasApplied;
    public readonly DamageRejectReason RejectReason;
    public readonly int AppliedDamage;
    public readonly bool WasKilled;
    public readonly bool WasTargetAlreadyDead;

    private DamageResult(bool wasApplied, DamageRejectReason rejectReason, int appliedDamage, bool wasKilled, bool wasTargetAlreadyDead)
    {
        WasApplied = wasApplied;
        RejectReason = rejectReason;
        AppliedDamage = appliedDamage;
        WasKilled = wasKilled;
        WasTargetAlreadyDead = wasTargetAlreadyDead;
    }

    // Health가 실제로 적용한 피해량, 최초 사망 전환 여부, 이번 타격 전에 이미 사망해 있었는지를 결과로 만든다.
    public static DamageResult Applied(int appliedDamage, bool wasKilled, bool wasTargetAlreadyDead)
    {
        return new DamageResult(true, DamageRejectReason.None, appliedDamage, wasKilled, wasTargetAlreadyDead);
    }

    // 체력을 변경하지 않고 피해 요청을 거부한 이유를 결과로 만든다.
    public static DamageResult Rejected(DamageRejectReason reason)
    {
        return new DamageResult(false, reason, 0, false, false);
    }
}
