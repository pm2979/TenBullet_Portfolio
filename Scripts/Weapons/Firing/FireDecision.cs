/// <summary>
/// 발사 승인 시 확인하고 소비할 자원 종류
/// </summary>
public enum FireCostType
{
    None,
    Health
}

/// <summary>
/// 발사 조건 평가 결과
/// </summary>
public enum FireReadiness
{
    /// <summary>발사 가능</summary>
    Ready,
    /// <summary>자원 부족, 선충전/쿨다운 중 등의 사유로 발사할 수 없음</summary>
    Blocked
}

/// <summary>
/// 선충전·쿨다운·자원 조건과 발사 예약 상태를 판정
/// </summary>
public sealed class FireDecision
{
    private float firePreChargeTimer;
    private float fireRateCoolDownTimer;

    /// <summary>
    /// 승인 후 성공 또는 실패가 확정되기 전까지 중복 승인을 막는 예약 상태
    /// </summary>
    public bool IsReserved { get; private set; }

    /// <summary>
    /// 연사 쿨다운을 경과 시간만큼 진행
    /// </summary>
    public void TickCoolDown(float deltaTime)
    {
        fireRateCoolDownTimer -= deltaTime;
    }

    /// <summary>
    /// 연속 입력이면 선충전을 진행하고 새 입력이면 준비 시간 초기화
    /// </summary>
    public void UpdatePreCharge(bool firePreviousFrame, float deltaTime, float weaponPrechargeTime)
    {
        if (firePreviousFrame)
        {
            firePreChargeTimer -= deltaTime;
        }
        else
        {
            firePreChargeTimer = weaponPrechargeTime;
        }
    }

    /// <summary>
    /// 선충전·예약·쿨다운·자원 조건을 포함한 발사 가능 여부 반환
    /// </summary>
    public FireReadiness Evaluate(FireCostType costType, Weapon weapon, int currentHealth)
    {
        if (firePreChargeTimer > 0f)
            return FireReadiness.Blocked;

        return EvaluateImmediate(costType, weapon, currentHealth);
    }

    /// <summary>
    /// 선충전을 제외하고 예약·쿨다운·자원 조건만으로 발사 가능 여부 반환
    /// </summary>
    public FireReadiness EvaluateImmediate(FireCostType costType, Weapon weapon, int currentHealth)
    {
        if (IsReserved)
            return FireReadiness.Blocked;

        if (costType == FireCostType.Health)
        {
            if (currentHealth < weapon.weaponDetails.healthCostPerFire)
                return FireReadiness.Blocked;
        }

        if (fireRateCoolDownTimer > 0f)
            return FireReadiness.Blocked;

        return FireReadiness.Ready;
    }

    /// <summary>
    /// 발사 결과가 확정될 때까지 중복 승인을 막는 예약 시작
    /// </summary>
    public void BeginReservation()
    {
        IsReserved = true;
    }

    /// <summary>
    /// 성공한 예약을 쿨다운 시작으로 확정하고 예약 해제
    /// </summary>
    public void ResetCoolDown(float weaponFireRate)
    {
        fireRateCoolDownTimer = weaponFireRate;
        IsReserved = false;
    }

    /// <summary>
    /// 실패한 예약을 쿨다운 없이 해제해 즉시 재시도 허용
    /// </summary>
    public void CancelReservation()
    {
        IsReserved = false;
    }

    /// <summary>
    /// 예약을 해제하고 다음 입력 세션이 첫 발 선충전을 다시 요구하도록 초기화
    /// </summary>
    public void ResetSession()
    {
        firePreChargeTimer = float.PositiveInfinity;
        IsReserved = false;
    }
}
