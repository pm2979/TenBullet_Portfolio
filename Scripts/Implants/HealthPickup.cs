using System;
using UnityEngine;

// 플레이어 접촉 또는 수명 만료 시 풀로 반환되는 체력 회복 오브젝트
[DisallowMultipleComponent]
public sealed class HealthPickup : MonoBehaviour
{
    private int _healAmount;
    private float _remainingLifetime;
    private Action<HealthPickup> _onReturned;
    private bool _isReturned;

    // 풀 대여 상태와 반환 콜백 초기화
    public void Initialize(int healAmount, float lifetime, Action<HealthPickup> onReturned)
    {
        _healAmount = healAmount;
        _remainingLifetime = Mathf.Max(0f, lifetime);
        _onReturned = onReturned;
        _isReturned = false;

        gameObject.SetActive(true);
    }

    private void Update()
    {
        _remainingLifetime -= Time.deltaTime;

        if (_remainingLifetime <= 0f)
        {
            ReturnToPool();
        }
    }

    // 플레이어 접촉 시 회복 적용 여부에 따라 Pickup 반환 처리
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (_isReturned)
        {
            return;
        }

        // 자식 Collider 접촉도 허용하되 플레이어 체력만 회복 대상으로 제한
        Health health = collision.GetComponentInParent<Health>();

        if (health == null || health.Faction != Faction.Player)
        {
            return;
        }

        HealResult healResult = health.TryRestoreHealth(new HealInfo(_healAmount));

        if (!healResult.WasApplied)
        {
            // 회복이 적용되지 않은 Pickup의 잔여 수명 유지
            return;
        }

        ReturnToPool();
    }

    // 중복 반환을 차단하고 활성 목록 정리 후 풀 반환 처리
    public void ReturnToPool()
    {
        if (_isReturned)
        {
            return;
        }

        _isReturned = true;

        Action<HealthPickup> onReturned = _onReturned;
        _onReturned = null;
        onReturned?.Invoke(this);

        if (PoolManager.HasInstance && PoolManager.Instance.ReturnComponent(this))
        {
            return;
        }

        gameObject.SetActive(false);
    }

    // 비활성화 직후의 중복 반환을 막기 위해 반환 상태를 유지한 채 대여 데이터만 정리
    private void OnDisable()
    {
        _remainingLifetime = 0f;
        _onReturned = null;
    }
}
