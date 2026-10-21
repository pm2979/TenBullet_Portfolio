using System.Collections.Generic;
using UnityEngine;

// 체력 Pickup의 안전 위치 생성과 활성 목록을 관리하는 생성기
public sealed class HealthPickupSpawner
{
    private const int MaxSafePositionAttempts = 6;
    private const float SafePositionSearchRadius = 0.6f;
    private const float SafePositionCheckRadius = 0.25f;

    private readonly GameObject _healthPickupPrefab;
    private readonly int _healAmount;
    private readonly float _pickupLifetime;
    private readonly int _maxActivePickup;
    private readonly LayerMask _obstacleLayerMask;

    private readonly List<HealthPickup> _activePickups = new List<HealthPickup>();

    // Pickup 생성 규칙과 풀 대여 설정 구성
    public HealthPickupSpawner(GameObject healthPickupPrefab, int healAmount, float pickupLifetime, int maxActivePickup, LayerMask obstacleLayerMask)
    {
        _healthPickupPrefab = healthPickupPrefab;
        _healAmount = healAmount;
        _pickupLifetime = pickupLifetime;
        _maxActivePickup = maxActivePickup;
        _obstacleLayerMask = obstacleLayerMask;
    }

    // 활성 상한과 안전 위치를 확인한 뒤 Pickup 대여 결과 반환
    public bool TrySpawn(Vector2 hitPosition)
    {
        if (_activePickups.Count >= _maxActivePickup)
        {
            return false;
        }

        if (_healthPickupPrefab == null)
        {
            Logger.LogWarning("HealthPickupSpawner: HealthPickup Prefab이 지정되지 않아 생성할 수 없습니다.");
            return false;
        }

        if (!PoolManager.HasInstance)
        {
            Logger.LogWarning("HealthPickupSpawner: PoolManager가 초기화되지 않아 HealthPickup을 생성할 수 없습니다.");
            return false;
        }

        if (!TryFindSafePosition(hitPosition, out Vector2 safePosition))
        {
            return false;
        }

        Component rentedComponent = PoolManager.Instance.RentComponent(_healthPickupPrefab, safePosition, Quaternion.identity);

        if (rentedComponent == null)
        {
            return false;
        }

        HealthPickup pickup = rentedComponent as HealthPickup;

        if (pickup == null)
        {
            PoolManager.Instance.ReturnComponent(rentedComponent);
            Logger.LogWarning($"HealthPickupSpawner: 대여한 Component '{rentedComponent}'가 HealthPickup이 아닙니다.", rentedComponent);
            return false;
        }

        _activePickups.Add(pickup);
        pickup.Initialize(_healAmount, _pickupLifetime, HandlePickupReturned);

        return true;
    }

    // 적중점 주변에서 장애물과 겹치지 않는 첫 생성 위치 탐색
    private bool TryFindSafePosition(Vector2 hitPosition, out Vector2 safePosition)
    {
        if (!Physics2D.OverlapCircle(hitPosition, SafePositionCheckRadius, _obstacleLayerMask))
        {
            safePosition = hitPosition;
            return true;
        }

        for (int attempt = 1; attempt < MaxSafePositionAttempts; attempt++)
        {
            Vector2 candidate = hitPosition + Random.insideUnitCircle * SafePositionSearchRadius;

            if (!Physics2D.OverlapCircle(candidate, SafePositionCheckRadius, _obstacleLayerMask))
            {
                safePosition = candidate;
                return true;
            }
        }

        safePosition = default;
        return false;
    }

    private void HandlePickupReturned(HealthPickup pickup)
    {
        _activePickups.Remove(pickup);
    }

    // 관리 중인 모든 Pickup의 일괄 반환
    public void ReturnAll()
    {
        // 반환 콜백이 원본 목록을 변경하므로 복사본 순회
        List<HealthPickup> pickupsToReturn = new List<HealthPickup>(_activePickups);

        for (int i = 0; i < pickupsToReturn.Count; i++)
        {
            pickupsToReturn[i].ReturnToPool();
        }
    }
}
