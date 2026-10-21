using UnityEngine;

/// <summary>주입된 Target의 생존·거리·속도·방향·최초 및 추적 시야를 계산</summary>
// Scene 검색 없이 Spawn Context로 전달받은 Target만 관찰
[DisallowMultipleComponent]
public sealed class EnemyTargetSensor : MonoBehaviour
{
    [Tooltip("시야 판정에서 시야를 가리는 장애물로 취급할 레이어. 비워두면(Nothing) 항상 시야가 확보된 것으로 본다")]
    [SerializeField] private LayerMask _obstacleLayerMask;

    private Transform _target;
    private Health _targetHealth;
    private Rigidbody2D _targetRigidbody;
    private float _detectionRange;
    private float _trackingSightRange;
    private Vector2 _previousTargetPosition;
    private float _previousSampleTime;
    private bool _hasPreviousSample;

    public TargetSnapshot Current { get; private set; } = TargetSnapshot.None;

    /// <summary>Target과 동일한 최초·추적 거리를 적용해 Sensor 초기화</summary>
    public void Initialize(Transform target, float detectionRange)
    {
        Initialize(target, detectionRange, detectionRange);
    }

    /// <summary>Target과 서로 다른 최초 탐지·추적 시야 거리를 적용해 Sensor 초기화</summary>
    public void Initialize(Transform target, float detectionRange, float trackingSightRange)
    {
        _target = target;
        _detectionRange = detectionRange;
        _trackingSightRange = trackingSightRange;
        _hasPreviousSample = false;
        _targetHealth = target != null ? target.GetComponent<Health>() : null;
        _targetRigidbody = target != null ? target.GetComponent<Rigidbody2D>() : null;

        Refresh();
    }

    /// <summary>Target 참조와 계산 이력을 제거해 미감지 상태로 초기화</summary>
    public void ClearTarget()
    {
        _target = null;
        _targetHealth = null;
        _targetRigidbody = null;
        _hasPreviousSample = false;
        Current = TargetSnapshot.None;
    }

    /// <summary>현재 Target 상태를 다시 측정해 하나의 일관된 TargetSnapshot 반환</summary>
    public TargetSnapshot Refresh()
    {
        if (!IsTargetUsable())
        {
            _hasPreviousSample = false;
            Current = TargetSnapshot.None;
            return Current;
        }

        Vector2 selfPosition = transform.position;
        Vector2 targetPosition = _target.position;
        Vector2 offset = targetPosition - selfPosition;
        float distance = offset.magnitude;
        Vector2 direction = distance > 0f ? offset / distance : Vector2.zero;
        Vector2 velocity = ComputeVelocity(targetPosition);
        bool inDetectionRange = distance <= _detectionRange;
        bool inTrackingSightRange = distance <= _trackingSightRange;

        // 최초·추적 범위가 겹쳐도 하나의 장애물 질의 결과를 공유
        bool hasVisiblePath = (inDetectionRange || inTrackingSightRange) &&
                              !Physics2D.Linecast(selfPosition, targetPosition, _obstacleLayerMask);
        bool hasLineOfSight = inDetectionRange && hasVisiblePath;
        bool hasTrackingLineOfSight = inTrackingSightRange && hasVisiblePath;

        Current = new TargetSnapshot(
            _target, targetPosition, velocity, direction, distance,
            hasLineOfSight, hasTrackingLineOfSight, inDetectionRange, isAlive: true);
        return Current;
    }

    // 탐지 거리와 분리해 Target 참조·활성·생존 상태만 확인
    private bool IsTargetUsable()
    {
        if (_target == null || !_target.gameObject.activeInHierarchy)
        {
            return false;
        }

        if (_targetHealth != null && _targetHealth.GetCurrentHealth() <= 0)
        {
            return false;
        }

        return true;
    }

    // Rigidbody2D 속도를 우선하고 없으면 이전 위치와 경과 시간으로 근사
    private Vector2 ComputeVelocity(Vector2 targetPosition)
    {
        if (_targetRigidbody != null)
        {
            return _targetRigidbody.linearVelocity;
        }

        float now = Time.time;

        if (!_hasPreviousSample)
        {
            _previousTargetPosition = targetPosition;
            _previousSampleTime = now;
            _hasPreviousSample = true;
            return Vector2.zero;
        }

        float deltaTime = now - _previousSampleTime;
        Vector2 velocity = deltaTime > 0f ? (targetPosition - _previousTargetPosition) / deltaTime : Vector2.zero;

        _previousTargetPosition = targetPosition;
        _previousSampleTime = now;

        return velocity;
    }
}
