using System;
using UnityEngine;

/// <summary>
/// 플레이어 스태미나의 초기화와 소비, 능력치 기반 회복을 관리한다.
/// </summary>
[DisallowMultipleComponent]
public class Stamina : MonoBehaviour
{
    private readonly StaminaState _staminaState = new StaminaState();
    private RuntimeStats _runtimeStats;
    private PlayerDataSO _playerDetails;

    public bool IsInitialized { get; private set; }
    public float Current => _staminaState.Current;
    public float Max => _staminaState.Max;

    /// <summary>
    /// 현재량 또는 최대치가 바뀌면 변경 원인과 함께 발생한다.
    /// </summary>
    public event Action<StaminaChange> Changed;

    /// <summary>
    /// 유효한 비용이 잔량 부족으로 거부되었을 때 발생한다.
    /// </summary>
    public event Action<StaminaConsumptionRejected> ConsumptionRejected;

    /// <summary>
    /// 런타임 능력치를 적용한 최대치로 완전 회복 상태를 한 번 초기화한다.
    /// </summary>
    public void Initialize(RuntimeStats runtimeStats, PlayerDataSO playerDetails)
    {
        if (IsInitialized)
        {
            Logger.LogWarning($"{nameof(Stamina)}가 이미 초기화된 상태에서 {nameof(Initialize)}가 다시 호출되어 무시합니다.", this);
            return;
        }

        if (runtimeStats == null)
        {
            throw new ArgumentNullException(nameof(runtimeStats));
        }

        if (playerDetails == null)
        {
            throw new ArgumentNullException(nameof(playerDetails));
        }

        _runtimeStats = runtimeStats;
        _playerDetails = playerDetails;
        _staminaState.Initialize(ResolveMax());

        IsInitialized = true;

        RaiseChanged(StaminaChangeReason.Initialized, 0f);
    }

    private void Update()
    {
        if (!IsInitialized) return;

        SyncMax();

        float recoveryRate = _runtimeStats.ApplyModifiers(StatType.StaminaRecoveryRate, _playerDetails.staminaRecoveryRate);
        float previousCurrent = _staminaState.Current;

        if (_staminaState.Tick(Time.deltaTime, recoveryRate))
        {
            RaiseChanged(StaminaChangeReason.Recovered, previousCurrent);
        }
    }

    /// <summary>
    /// 비용을 원자적으로 확인·소비하고 회복 지연을 다시 시작한다.
    /// 유효하지 않은 비용이나 잔량 부족은 상태를 바꾸지 않는다.
    /// </summary>
    public bool TryConsume(float cost)
    {
        // 소비 직전 최신 최대치를 동기화해 현재량 제한과 비용 판정의 기준을 맞춘다.
        SyncMax();

        float previousCurrent = _staminaState.Current;

        if (!_staminaState.TryConsume(cost, _playerDetails.staminaRecoveryDelay))
        {
            if (IsValidCost(cost) && cost > _staminaState.Current)
            {
                ConsumptionRejected?.Invoke(new StaminaConsumptionRejected(cost, _staminaState.Current, _staminaState.Max));
            }

            return false;
        }

        RaiseChanged(StaminaChangeReason.Consumed, previousCurrent);
        return true;
    }

    private float ResolveMax()
    {
        return _runtimeStats.ApplyModifiers(StatType.MaxStamina, _playerDetails.maxStamina);
    }

    // 최종 최대치가 바뀐 경우에만 상태를 갱신하며, 증가할 때는 현재량을 유지한다.
    private void SyncMax()
    {
        float finalValue = ResolveMax();

        if (finalValue == _staminaState.Max) return;

        float previousCurrent = _staminaState.Current;
        _staminaState.SetMax(finalValue);
        RaiseChanged(StaminaChangeReason.MaxChanged, previousCurrent);
    }

    private void RaiseChanged(StaminaChangeReason reason, float previousCurrent)
    {
        float currentValue = _staminaState.Current;

        Changed?.Invoke(new StaminaChange(previousCurrent, currentValue, _staminaState.Max, currentValue - previousCurrent, reason));
    }

    private static bool IsValidCost(float cost)
    {
        return float.IsFinite(cost) && cost >= 0f;
    }
}
