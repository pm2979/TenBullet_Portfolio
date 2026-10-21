using System;
using UnityEngine;

/// <summary>
/// 패링 판정 시간을 관리하고 패링 가능한 첫 피해를 소진한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerParry : MonoBehaviour
{
    [Tooltip("패링 시간·비용 base를 소유한 설정 데이터")]
    [SerializeField] private ParryConfigSO _config;

    /// <summary>패링 판정이 활성 상태면 true</summary>
    public bool IsActive { get; private set; }

    /// <summary>현재 패링의 남은 판정 시간(초). 비활성 상태에서는 0이다</summary>
    public float RemainingWindow { get; private set; }

    /// <summary>패링 시간·비용 base를 소유한 설정 데이터. 연결되지 않았으면 null이며 이때 패링을 시작할 수 없다</summary>
    public ParryConfigSO Config => _config;

    /// <summary>패링이 시작되어 판정이 활성화된 직후 발생한다</summary>
    public event Action Started;

    /// <summary>패링이 피해를 무효화했을 때 공격의 유입 방향과 함께 발생한다. 항상 Ended보다 먼저 발행된다</summary>
    public event Action<Vector2> Succeeded;

    /// <summary>성공·만료·취소·비활성화 중 어떤 이유로든 패링이 끝났을 때 정확히 한 번 발생한다</summary>
    public event Action Ended;

    private void Update()
    {
        if (!IsActive) return;

        RemainingWindow -= Time.deltaTime;

        if (RemainingWindow <= 0f)
        {
            Cancel();
        }
    }

    private void OnDisable()
    {
        // 비활성화 시에도 종료 이벤트를 거쳐 판정과 연출 상태를 함께 정리한다.
        Cancel();
    }

    /// <summary>
    /// 비용 소비 전에 주어진 판정 시간으로 패링을 시작할 수 있는지 확인한다.
    /// </summary>
    public bool CanStart(float finalWindow)
    {
        return !IsActive && float.IsFinite(finalWindow) && finalWindow > 0f;
    }

    /// <summary>
    /// 확정된 판정 시간을 저장하고 패링을 즉시 활성화한다.
    /// </summary>
    public bool TryStart(float finalWindow)
    {
        if (!CanStart(finalWindow))
        {
            return false;
        }

        IsActive = true;
        RemainingWindow = finalWindow;

        Started?.Invoke();
        return true;
    }

    /// <summary>
    /// 패링 가능한 첫 피해를 무효화하고 성공 이벤트와 종료 이벤트를 순서대로 발생시킨다.
    /// </summary>
    public bool TryParry(in DamageInfo damageInfo)
    {
        if (!IsActive || !damageInfo.IsParryable)
        {
            return false;
        }

        EndParry(true, damageInfo.IncomingDirection);
        return true;
    }

    /// <summary>
    /// 진행 중인 패링을 성공 없이 종료하며, 비활성 상태의 호출은 무시한다.
    /// </summary>
    public void Cancel()
    {
        EndParry(false, Vector2.zero);
    }

    // 상태를 먼저 끈 뒤 이벤트를 발행해 중복 호출과 재진입에서도 한 번만 종료한다.
    private void EndParry(bool wasSuccessful, Vector2 incomingDirection)
    {
        if (!IsActive)
        {
            return;
        }

        IsActive = false;
        RemainingWindow = 0f;

        if (wasSuccessful)
        {
            Succeeded?.Invoke(incomingDirection);
        }

        Ended?.Invoke();
    }
}
