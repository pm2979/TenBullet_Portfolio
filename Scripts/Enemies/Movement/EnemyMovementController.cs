using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Enemy 이동 의도·Navigation 조향·잠금·넉백 우선순위를 실제 Motor 속도로 변환</summary>
// Rigidbody2D 변경은 VelocityMotor2D에만 위임하고 몸체 Collider 기준 좌표 보정은 이 경계에서만 수행
[RequireComponent(typeof(VelocityMotor2D))]
[DisallowMultipleComponent]
public sealed class EnemyMovementController : MonoBehaviour
{
    // MoveTowards의 벽·PlayerBody 접촉 정책을 고정하는 Cast Layer
    private const string WallLayerName = "Wall";
    private const string PlayerBodyLayerName = "PlayerBody";

    // 접촉면 재충돌을 피하기 위한 Cast 여유 거리
    private const float CollisionSkin = 0.02f;

    [Tooltip("벽 충돌·Navigation 보정 기준이 되는 Root의 물리 Body Collider. 비워 두면 Root의 첫 non-trigger Collider2D를 사용한다")]
    [SerializeField] private Collider2D _bodyCollider;

    private VelocityMotor2D _velocityMotor;
    private IEnemyNavigation _navigation;
    private ContactFilter2D _blockContactFilter;
    private readonly List<RaycastHit2D> _castResultsBuffer = new List<RaycastHit2D>(4);

    private bool _hasNavigationIntent;
    private Vector2 _targetPosition;
    private float _intentSpeed;

    private bool _isLocked;
    private bool _isKnockedBack;
    private bool _isMoving;
    private Vector2 _lastAppliedDirection;
    private Vector2 _lastFacingDirection;
    private bool _hasFacingDirection;

    public bool IsMoving => _isMoving;
    public bool IsLocked => _isLocked;
    public bool IsKnockedBack => _isKnockedBack;

    /// <summary>Navigation을 사용할 목표 이동 의도 보유 여부</summary>
    public bool HasNavigationIntent => _hasNavigationIntent;

    /// <summary>Decision이 도착·경로 실패를 판단할 마지막 Navigation 상태</summary>
    public EnemyNavigationStatus LastNavigationStatus { get; private set; } = EnemyNavigationStatus.NotReady;

    /// <summary>실제 Motor 이동 방향이 변경될 때 발행</summary>
    public event Action<Vector2> MovementChanged;

    /// <summary>실제 Motor가 이동에서 정지로 전환될 때 발행</summary>
    public event Action MovementStopped;

    /// <summary>물리 이동과 무관한 Visual 바라보기 방향 변경 시 발행</summary>
    public event Action<Vector2> FacingChanged;

    private void Awake()
    {
        _velocityMotor = GetComponent<VelocityMotor2D>();
        _bodyCollider = ResolveBodyCollider();

        _blockContactFilter.SetLayerMask(LayerMask.GetMask(WallLayerName, PlayerBodyLayerName));
        _blockContactFilter.useTriggers = false;
    }

    // 명시 참조를 우선하고 없으면 공격 Trigger를 제외한 루트 물리 Collider 선택
    private Collider2D ResolveBodyCollider()
    {
        if (_bodyCollider != null)
        {
            return _bodyCollider;
        }

        Collider2D[] colliders = GetComponents<Collider2D>();

        for (int i = 0; i < colliders.Length; i++)
        {
            if (!colliders[i].isTrigger)
            {
                return colliders[i];
            }
        }

        return null;
    }

    /// <summary>Transform 원점과 실제 몸체 Collider 중심 사이의 Navigation 좌표 보정값</summary>
    private Vector2 BodyOffset => _bodyCollider != null ? _bodyCollider.offset : Vector2.zero;

    private void OnDisable()
    {
        // 비활성화와 Pool 반환 뒤 물리 속도 잔존 방지
        _isKnockedBack = false;
        StopInternal(notifyStopped: false, facingDirection: Vector2.zero);
    }

    private void FixedUpdate()
    {
        // 넉백 중 AI 의도 적용만 보류하고 최신 의도 자체는 보존
        if (_isKnockedBack)
        {
            return;
        }

        if (_isLocked || !_hasNavigationIntent)
        {
            // 의도 없음과 잠금 상태에서도 Motor 정지를 재확정해 외부 속도 잔존 방지
            StopMotorOnly(Vector2.zero);
            return;
        }

        Vector2 selfPosition = transform.position;
        ApplyNavigationResult(ResolveNavigationResult(selfPosition), ResolveTowardsDirection(selfPosition));
    }

    /// <summary>이번 대여에서 사용할 Navigation 구현을 명시적으로 설정</summary>
    public void SetNavigation(IEnemyNavigation navigation)
    {
        _navigation = navigation;
    }

    /// <summary>다음 FixedUpdate부터 적용할 목표 위치와 이동 속도 저장</summary>
    public void MoveTowards(Vector2 targetPosition, float speed)
    {
        if (_isLocked)
        {
            return;
        }

        _hasNavigationIntent = true;
        _targetPosition = targetPosition;
        _intentSpeed = speed;
    }

    /// <summary>잠금과 무관하게 이동 의도를 지우고 Motor를 즉시 정지</summary>
    public void Stop(Vector2 facingDirection)
    {
        StopInternal(notifyStopped: true, facingDirection: facingDirection);
    }

    /// <summary>공격 단계의 이동 허용 여부를 반영하고 잠금 시 기존 의도 제거</summary>
    public void SetMovementLocked(bool isLocked, Vector2 facingDirection)
    {
        _isLocked = isLocked;

        if (isLocked)
        {
            StopInternal(notifyStopped: true, facingDirection: facingDirection);
        }
    }

    /// <summary>AI 의도와 바라보기 방향을 보존한 채 잠금보다 우선하는 넉백 속도 적용</summary>
    public void ApplyKnockback(Vector2 direction, float speed)
    {
        _isKnockedBack = true;

        _velocityMotor.SetVelocity(direction, speed);

        bool directionChanged = !_isMoving || direction != _lastAppliedDirection;
        _isMoving = true;

        if (directionChanged)
        {
            _lastAppliedDirection = direction;
            MovementChanged?.Invoke(direction);
        }
    }

    /// <summary>넉백 속도를 정지하고 다음 FixedUpdate에서 보존된 최신 AI 의도 재개</summary>
    public void EndKnockback()
    {
        _isKnockedBack = false;
        StopMotorOnly(Vector2.zero);
    }

    /// <summary>물리 속도를 바꾸지 않고 유효한 바라보기 방향만 갱신</summary>
    public void SetFacingDirection(Vector2 direction)
    {
        if (direction.sqrMagnitude <= 0f)
        {
            return;
        }

        Vector2 normalizedDirection = direction.normalized;

        if (_hasFacingDirection && normalizedDirection == _lastFacingDirection)
        {
            return;
        }

        _hasFacingDirection = true;
        _lastFacingDirection = normalizedDirection;
        FacingChanged?.Invoke(normalizedDirection);
    }

    /// <summary>Navigation 참조와 Dungeon Context를 유지한 채 현재 경로만 취소</summary>
    public void CancelNavigationPath()
    {
        _navigation?.CancelCurrentPath();
        LastNavigationStatus = EnemyNavigationStatus.NotReady;
    }

    /// <summary>몸체 좌표로 보정해 가장 가까운 이동 가능 위치 질의</summary>
    public bool TryGetNearestWalkablePosition(Vector2 desiredPosition, out Vector2 walkablePosition)
    {
        if (_navigation == null)
        {
            walkablePosition = desiredPosition;
            return false;
        }

        // 몸체 좌표로 질의하고 결과를 외부 계약인 Transform 좌표로 복원
        Vector2 bodyOffset = BodyOffset;

        if (!_navigation.TryGetNearestWalkablePosition(desiredPosition + bodyOffset, out Vector2 walkableBodyPosition))
        {
            walkablePosition = desiredPosition;
            return false;
        }

        walkablePosition = walkableBodyPosition - bodyOffset;
        return true;
    }

    /// <summary>Pool 재사용을 위해 의도·속도·잠금·방향과 Navigation 대여 상태를 초기화</summary>
    public void ResetRuntimeState()
    {
        _isLocked = false;
        _isKnockedBack = false;
        StopInternal(notifyStopped: false, facingDirection: Vector2.zero);

        _hasFacingDirection = false;
        _lastFacingDirection = Vector2.zero;

        _navigation?.ResetRuntimeState();
        _navigation = null;
        LastNavigationStatus = EnemyNavigationStatus.NotReady;
    }

    // Navigation 누락을 직선 이동으로 대체하지 않고 NotReady 정지로 처리
    private EnemyNavigationResult ResolveNavigationResult(Vector2 selfPosition)
    {
        if (_navigation == null)
        {
            return EnemyNavigationResult.NotReady;
        }

        // 출발·목적지에 같은 Body Offset을 적용해 Graph 질의 평면 일치
        Vector2 bodyOffset = BodyOffset;

        return _navigation.GetSteeringResult(selfPosition + bodyOffset, _targetPosition + bodyOffset);
    }

    // 조향 대기 중에도 Target을 바라보도록 Transform 기준 직접 방향 계산
    private Vector2 ResolveTowardsDirection(Vector2 selfPosition)
    {
        Vector2 offset = _targetPosition - selfPosition;
        return offset.sqrMagnitude > 0f ? offset.normalized : Vector2.zero;
    }

    // Success만 Cast 이동으로 적용하고 나머지는 재시도할 의도를 보존한 채 Motor 정지
    private void ApplyNavigationResult(EnemyNavigationResult result, Vector2 facingDirection)
    {
        LastNavigationStatus = result.Status;

        if (result.Status == EnemyNavigationStatus.Success)
        {
            ApplyVelocityWithCollisionSlide(result.DesiredDirection);
            return;
        }

        StopMotorOnly(facingDirection);
    }

    // 몸체 Cast로 이번 스텝의 허용 변위를 구하고 막힌 축만 제거해 벽을 따라 이동
    // 실제 이동이 미끄러져도 바라보기는 원래 Navigation 조향 방향으로 유지
    private void ApplyVelocityWithCollisionSlide(Vector2 direction)
    {
        Vector2 rawDisplacement = direction * (_intentSpeed * Time.fixedDeltaTime);

        if (rawDisplacement == Vector2.zero)
        {
            ApplyVelocity(direction, _intentSpeed);
            return;
        }

        float fullDistance = rawDisplacement.magnitude;

        if (CastAllowedDistance(rawDisplacement) >= fullDistance)
        {
            ApplyVelocity(direction, _intentSpeed);
            return;
        }

        Vector2 slidDisplacement = ResolveSlideDisplacement(rawDisplacement);

        if (slidDisplacement == Vector2.zero)
        {
            StopMotorOnly(direction);
            return;
        }

        float slidDistance = slidDisplacement.magnitude;
        ApplyVelocity(slidDisplacement / slidDistance, slidDistance / Time.fixedDeltaTime, direction);
    }

    // 수평·수직 허용 변위를 독립 계산해 통과 가능한 축만 합산
    private Vector2 ResolveSlideDisplacement(Vector2 rawDisplacement)
    {
        Vector2 result = Vector2.zero;

        Vector2 horizontalComponent = new Vector2(rawDisplacement.x, 0f);
        if (horizontalComponent != Vector2.zero)
        {
            result += horizontalComponent.normalized * CastAllowedDistance(horizontalComponent);
        }

        Vector2 verticalComponent = new Vector2(0f, rawDisplacement.y);
        if (verticalComponent != Vector2.zero)
        {
            result += verticalComponent.normalized * CastAllowedDistance(verticalComponent);
        }

        return result;
    }

    // Body Collider Cast로 Wall·PlayerBody에 닿기 전까지의 이동 가능 거리 계산
    private float CastAllowedDistance(Vector2 displacement)
    {
        float distance = displacement.magnitude;

        if (distance <= 0f)
        {
            return 0f;
        }

        // Body Collider가 없는 테스트 구성은 차단 없이 원래 변위 허용
        if (_bodyCollider == null)
        {
            return distance;
        }

        Vector2 direction = displacement / distance;
        int hitCount = _bodyCollider.Cast(direction, _blockContactFilter, _castResultsBuffer, distance + CollisionSkin);
        float allowedDistance = distance;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hit = _castResultsBuffer[i];

            if (hit.distance <= 0f && Vector2.Dot(direction, hit.normal) > 0f)
            {
                continue;
            }

            float candidateDistance = Mathf.Max(0f, hit.distance - CollisionSkin);

            if (candidateDistance < allowedDistance)
            {
                allowedDistance = candidateDistance;
            }
        }

        return allowedDistance;
    }

    private void ApplyVelocity(Vector2 direction)
    {
        ApplyVelocity(direction, _intentSpeed, direction);
    }

    private void ApplyVelocity(Vector2 direction, float speed)
    {
        ApplyVelocity(direction, speed, direction);
    }

    private void ApplyVelocity(Vector2 direction, float speed, Vector2 facingDirection)
    {
        SetFacingDirection(facingDirection);
        _velocityMotor.SetVelocity(direction, speed);

        bool directionChanged = !_isMoving || direction != _lastAppliedDirection;

        _isMoving = true;

        if (directionChanged)
        {
            _lastAppliedDirection = direction;
            MovementChanged?.Invoke(direction);
        }
    }

    // 저장된 의도를 보존한 채 Motor와 이동 이벤트 상태만 정지로 동기화
    private void StopMotorOnly(Vector2 facingDirection)
    {
        _velocityMotor.Stop();
        SetFacingDirection(facingDirection);

        if (!_isMoving)
        {
            return;
        }

        _isMoving = false;
        _lastAppliedDirection = Vector2.zero;
        MovementStopped?.Invoke();
    }

    private void StopInternal(bool notifyStopped, Vector2 facingDirection)
    {
        _velocityMotor.Stop();
        _hasNavigationIntent = false;
        _targetPosition = Vector2.zero;
        _intentSpeed = 0f;
        SetFacingDirection(facingDirection);

        if (!_isMoving)
        {
            return;
        }

        _isMoving = false;
        _lastAppliedDirection = Vector2.zero;

        if (notifyStopped)
        {
            MovementStopped?.Invoke();
        }
    }
}
