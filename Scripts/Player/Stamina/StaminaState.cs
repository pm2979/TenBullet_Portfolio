/// <summary>
/// 스태미나 현재량과 최대치, 회복 지연 시간을 관리한다.
/// </summary>
public sealed class StaminaState
{
    public float Current { get; private set; }
    public float Max { get; private set; }
    public float RecoveryDelayRemaining { get; private set; }

    /// <summary>
    /// 지정한 최대치로 완전 회복 상태를 초기화한다.
    /// </summary>
    public void Initialize(float max)
    {
        Max = max;
        Current = max;
        RecoveryDelayRemaining = 0f;
    }

    /// <summary>
    /// 최대치를 갱신하며 현재량은 새 최대치를 넘는 경우에만 줄인다.
    /// </summary>
    public void SetMax(float newMax)
    {
        Max = newMax;

        if (Current > Max)
        {
            Current = Max;
        }
    }

    /// <summary>
    /// 유효한 비용을 잔량에서 소비하고 회복 지연을 다시 시작한다.
    /// 비용이 유효하지 않거나 잔량이 부족하면 상태를 바꾸지 않는다.
    /// </summary>
    public bool TryConsume(float cost, float recoveryDelay)
    {
        if (!float.IsFinite(cost) || cost < 0f)
        {
            return false;
        }

        if (cost > Current)
        {
            return false;
        }

        Current -= cost;
        RecoveryDelayRemaining = recoveryDelay;

        return true;
    }

    /// <summary>
    /// 회복 지연을 진행한 뒤 회복량을 적용하고, 현재량이 바뀌었는지 반환한다.
    /// </summary>
    public bool Tick(float deltaTime, float recoveryRate)
    {
        if (Current >= Max)
        {
            return false;
        }

        if (RecoveryDelayRemaining > 0f)
        {
            RecoveryDelayRemaining -= deltaTime;

            if (RecoveryDelayRemaining < 0f)
            {
                RecoveryDelayRemaining = 0f;
            }

            return false;
        }

        float previousCurrent = Current;
        float recovered = Current + (recoveryRate * deltaTime);
        Current = recovered > Max ? Max : recovered;

        return Current != previousCurrent;
    }
}
