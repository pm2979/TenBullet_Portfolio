using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어의 충돌 제한 이동과 회피, 발사 반동을 실행한다.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(VelocityMotor2D))]
[RequireComponent(typeof(PositionMotor2D))]
[DisallowMultipleComponent]
public class PlayerMotor2D : MonoBehaviour
{
    [Tooltip("구르기 속도·거리 정보 MovementDataSO")]
    [SerializeField] private MovementDataSO _movementData;

    private const float DodgeArrivalDistance = 0.001f;

    // 일반 이동은 벽과 적 본체만 차단 대상으로 취급한다.
    private const string WallLayerName = "Wall";
    private const string EnemyBodyLayerName = "EnemyBody";

    // 접촉면에서 미세하게 겹치거나 떨리지 않도록 이동 가능 거리에 여유를 둔다.
    private const float CollisionSkin = 0.02f;

    private Rigidbody2D _rigidBody2D;
    private VelocityMotor2D _velocityMotor2D;
    private PositionMotor2D _positionMotor2D;
    private BoxCollider2D _bodyCollider;
    private PlayerController _playerController;
    private KnockbackReceiver _knockbackReceiver;
    private readonly DodgeState _dodgeState = new DodgeState();
    private Coroutine _dodgeCoroutine;
    private Coroutine _movementRecoilCoroutine;
    private WaitForFixedUpdate _waitForFixedUpdate;
    private float _dodgeMoveSpeed;
    private float _dodgeDuration;
    private float _dodgeElapsed;

    // 일반 이동 의도는 물리 스텝에서 검사하며, 강제 이동이 시작되면 즉시 폐기한다.
    private bool _hasMoveIntent;
    private Vector2 _moveIntentDirection;
    private float _moveIntentSpeed;
    private ContactFilter2D _collisionContactFilter;
    private readonly List<RaycastHit2D> _castResultsBuffer = new List<RaycastHit2D>(4);

    // 적과의 물리 접촉이 비활성화되어 회피 중 적 본체 충돌은 직접 검사한다.
    private ContactFilter2D _enemyBodyContactFilter;
    private readonly List<Collider2D> _enemyOverlapBuffer = new List<Collider2D>(2);

    public bool IsDodging => _dodgeState.IsRolling;
    public bool CanDodge => _dodgeState.CanStart;
    public float ActiveDodgeDuration => _dodgeDuration;
    public float ActiveDodgeInvulnerabilityDuration { get; private set; }

    /// <summary>
    /// 발사 이동 반동이 진행 중인지 나타낸다.
    /// </summary>
    public bool IsMovementRecoiling => _movementRecoilCoroutine != null;

    /// <summary>
    /// 회피가 시작되면 적용 방향과 지속 시간으로 발생한다.
    /// </summary>
    public event Action<Vector2, float> DodgeStarted;

    /// <summary>
    /// 회피가 정상 종료되거나 취소되면 발생한다.
    /// </summary>
    public event Action DodgeEnded;

    /// <summary>
    /// 이동 명령이 적용되면 방향과 함께 발생한다.
    /// </summary>
    public event Action<Vector2> MovementChanged;

    /// <summary>
    /// 이동 명령이 정지 상태로 바뀌면 발생한다.
    /// </summary>
    public event Action MovementStopped;

    private void Awake()
    {
        _rigidBody2D = GetComponent<Rigidbody2D>();
        _velocityMotor2D = GetComponent<VelocityMotor2D>();
        _positionMotor2D = GetComponent<PositionMotor2D>();
        _bodyCollider = GetComponent<BoxCollider2D>();
        _playerController = GetComponent<PlayerController>();
        _knockbackReceiver = GetComponent<KnockbackReceiver>();
        _waitForFixedUpdate = new WaitForFixedUpdate();

        _collisionContactFilter.SetLayerMask(LayerMask.GetMask(WallLayerName, EnemyBodyLayerName));
        _collisionContactFilter.useTriggers = false;

        _enemyBodyContactFilter.SetLayerMask(LayerMask.GetMask(EnemyBodyLayerName));
        _enemyBodyContactFilter.useTriggers = false;
    }

    private void OnDisable()
    {
        CancelDodge();
        CancelMovementRecoil();
        Stop();
    }

    private void FixedUpdate()
    {
        // 회피·반동·넉백이 속도를 소유하는 동안 일반 이동이 이를 덮어쓰지 않게 한다.
        if (!_hasMoveIntent || _dodgeState.IsRolling || IsMovementRecoiling ||
            (_knockbackReceiver != null && _knockbackReceiver.IsKnockedBack))
        {
            return;
        }

        ApplyCollisionLimitedVelocity();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        CancelDodge();
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        CancelDodge();
    }

    /// <summary>
    /// 충돌 제한 없이 속도를 즉시 적용하고 일반 이동 의도를 폐기한다.
    /// </summary>
    public void Move(Vector2 direction, float speed)
    {
        _hasMoveIntent = false;
        _velocityMotor2D.SetVelocity(direction, speed);
        MovementChanged?.Invoke(direction);
    }

    /// <summary>
    /// 일반 이동 의도를 저장하고 이동 표현을 즉시 알리며, 실제 속도는 다음 물리 스텝에 확정한다.
    /// </summary>
    public void MoveWithCollisionCheck(Vector2 direction, float speed)
    {
        _hasMoveIntent = true;
        _moveIntentDirection = direction;
        _moveIntentSpeed = speed;
        MovementChanged?.Invoke(direction);
    }

    /// <summary>
    /// 일반 이동 의도를 지우고 속도를 0으로 만든 뒤 정지 상태를 알린다.
    /// </summary>
    public void Stop()
    {
        _hasMoveIntent = false;
        _velocityMotor2D.Stop();
        MovementStopped?.Invoke();
    }

    // 이번 물리 스텝의 허용 거리를 계산하고, 대각선이 막히면 이동 가능한 축으로 미끄러뜨린다.
    private void ApplyCollisionLimitedVelocity()
    {
        Vector2 rawDisplacement = _moveIntentDirection * _moveIntentSpeed * Time.fixedDeltaTime;

        if (rawDisplacement == Vector2.zero)
        {
            _velocityMotor2D.SetVelocity(_moveIntentDirection, _moveIntentSpeed);
            return;
        }

        float fullDistance = rawDisplacement.magnitude;
        float allowedDistance = CastAllowedDistance(rawDisplacement);

        if (allowedDistance >= fullDistance)
        {
            _velocityMotor2D.SetVelocity(_moveIntentDirection, _moveIntentSpeed);
            return;
        }

        Vector2 slidDisplacement = ResolveSlideDisplacement(rawDisplacement);

        if (slidDisplacement == Vector2.zero)
        {
            _velocityMotor2D.Stop();
            return;
        }

        Vector2 appliedDirection = slidDisplacement / slidDisplacement.magnitude;
        float appliedSpeed = slidDisplacement.magnitude / Time.fixedDeltaTime;
        _velocityMotor2D.SetVelocity(appliedDirection, appliedSpeed);
    }

    // 대각선 변위를 축별로 검사해 막히지 않은 성분만 합산한다.
    private Vector2 ResolveSlideDisplacement(Vector2 rawDisplacement)
    {
        Vector2 result = Vector2.zero;

        Vector2 horizontalComponent = new Vector2(rawDisplacement.x, 0f);
        if (horizontalComponent != Vector2.zero)
        {
            float allowed = CastAllowedDistance(horizontalComponent);
            result += horizontalComponent.normalized * allowed;
        }

        Vector2 verticalComponent = new Vector2(0f, rawDisplacement.y);
        if (verticalComponent != Vector2.zero)
        {
            float allowed = CastAllowedDistance(verticalComponent);
            result += verticalComponent.normalized * allowed;
        }

        return result;
    }

    // 충돌면까지의 허용 거리를 반환하며, 이미 겹친 대상에서 빠져나가는 이동은 허용한다.
    private float CastAllowedDistance(Vector2 displacement)
    {
        float distance = displacement.magnitude;

        if (distance <= 0f)
        {
            return 0f;
        }

        Vector2 direction = displacement / distance;
        int hitCount = _bodyCollider.Cast(direction, _collisionContactFilter, _castResultsBuffer, distance + CollisionSkin);
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

    /// <summary>
    /// 비용 소비 전에 회피 상태와 이동 수치가 시작 가능한지 확인한다.
    /// </summary>
    public bool CanStartDodge(Vector2 direction, float distance)
    {
        return CanStartDodge(direction, distance, _movementData.DodgeDuration, _movementData.DodgeInvulnerabilityDuration);
    }

    /// <summary>
    /// 지정한 거리·지속 시간·무적 시간으로 회피를 시작할 수 있는지 확인한다.
    /// </summary>
    public bool CanStartDodge(Vector2 direction, float distance, float duration, float invulnerabilityDuration)
    {
        return _dodgeState.CanStart && direction.sqrMagnitude > 0f && float.IsFinite(direction.x) &&
               float.IsFinite(direction.y) && float.IsFinite(distance) && distance > 0f &&
               float.IsFinite(duration) && duration > 0f && float.IsFinite(invulnerabilityDuration) &&
               invulnerabilityDuration >= 0f;
    }

    /// <summary>
    /// 이동 설정의 기본 수치로 회피를 시작한다.
    /// </summary>
    public bool TryStartDodge(Vector2 direction)
    {
        return TryStartDodge(
            direction,
            _movementData.DodgeDistance,
            _movementData.DodgeDuration,
            _movementData.DodgeInvulnerabilityDuration);
    }

    /// <summary>
    /// 지정한 거리와 이동 설정의 시간 수치로 회피를 시작한다.
    /// </summary>
    public bool TryStartDodge(Vector2 direction, float distance)
    {
        return TryStartDodge(direction, distance, _movementData.DodgeDuration, _movementData.DodgeInvulnerabilityDuration);
    }

    /// <summary>
    /// 시작 시점에 확정된 거리·지속 시간·무적 시간으로 회피를 시작한다.
    /// </summary>
    public bool TryStartDodge(Vector2 direction, float distance, float duration, float invulnerabilityDuration)
    {
        if (!CanStartDodge(direction, distance, duration, invulnerabilityDuration) ||
            !_dodgeState.TryStart(_rigidBody2D.position, direction, distance))
        {
            return false;
        }

        _dodgeMoveSpeed = distance / duration;
        _dodgeDuration = duration;
        _dodgeElapsed = 0f;
        ActiveDodgeInvulnerabilityDuration = Mathf.Min(invulnerabilityDuration, duration);

        _dodgeCoroutine = StartCoroutine(DodgeRoutine());
        DodgeStarted?.Invoke(direction, _dodgeDuration);
        return true;
    }

    /// <summary>
    /// 진행 중인 회피를 즉시 취소하고 이동 상태와 확정 수치를 정리한다.
    /// </summary>
    public void CancelDodge()
    {
        if (_dodgeCoroutine == null) return;

        StopCoroutine(_dodgeCoroutine);
        _dodgeCoroutine = null;
        _dodgeState.Cancel();
        Stop();
        ClearDodgeSnapshot();

        DodgeEnded?.Invoke();
    }

    private IEnumerator DodgeRoutine()
    {
        while (_dodgeElapsed < _dodgeDuration &&
               _dodgeState.GetRemainingDistance(_rigidBody2D.position) > DodgeArrivalDistance)
        {
            // 벽은 물리 충돌에 맡기고, 충돌하지 않는 적 본체만 별도로 검사한다.
            if (IsBlockedByEnemyBody())
            {
                CancelDodge();
                yield break;
            }

            _positionMotor2D.MoveTowards(_dodgeState.TargetPosition, _dodgeMoveSpeed);

            yield return _waitForFixedUpdate;
            _dodgeElapsed += Time.fixedDeltaTime;
        }

        _dodgeState.Stop();
        _dodgeCoroutine = null;
        ClearDodgeSnapshot();

        DodgeEnded?.Invoke();
    }

    // 적 본체와 이미 겹쳤거나 다음 이동 구간에서 닿는 경우 모두 회피를 막는다.
    private bool IsBlockedByEnemyBody()
    {
        if (_bodyCollider.Overlap(_enemyBodyContactFilter, _enemyOverlapBuffer) > 0)
        {
            return true;
        }

        Vector2 toTarget = _dodgeState.TargetPosition - _rigidBody2D.position;

        if (toTarget.sqrMagnitude <= 0f)
        {
            return false;
        }

        Vector2 direction = toTarget.normalized;
        float distance = _dodgeMoveSpeed * Time.fixedDeltaTime;

        return _bodyCollider.Cast(direction, _enemyBodyContactFilter, _castResultsBuffer, distance) > 0;
    }

    private void ClearDodgeSnapshot()
    {
        _dodgeMoveSpeed = 0f;
        _dodgeDuration = 0f;
        _dodgeElapsed = 0f;
        ActiveDodgeInvulnerabilityDuration = 0f;
    }

    /// <summary>
    /// 유효한 발사 반동을 시작하며, 기존 반동은 중첩하지 않고 새 요청으로 교체한다.
    /// 회피나 피격 넉백이 진행 중이면 해당 강제 이동을 우선한다.
    /// </summary>
    public bool TryStartMovementRecoil(Vector2 direction, float speed, float duration)
    {
        if (direction.sqrMagnitude <= 0f || speed <= 0f || duration <= 0f)
            return false;

        if (_dodgeState.IsRolling)
            return false;

        if (_knockbackReceiver != null && _knockbackReceiver.IsKnockedBack)
            return false;

        CancelMovementRecoil();

        _movementRecoilCoroutine = StartCoroutine(MovementRecoilRoutine(direction.normalized, speed, duration));
        return true;
    }

    /// <summary>
    /// 진행 중인 발사 반동을 취소하고 이동 잠금만 해제한다.
    /// </summary>
    public void CancelMovementRecoil()
    {
        if (_movementRecoilCoroutine == null) return;

        StopCoroutine(_movementRecoilCoroutine);
        _movementRecoilCoroutine = null;
        _playerController?.SetMovementLocked(false);
    }

    private IEnumerator MovementRecoilRoutine(Vector2 direction, float speed, float duration)
    {
        // 반동 속도가 일반 이동 입력에 덮어쓰이지 않도록 지속 시간 동안 이동 입력을 잠근다.
        _playerController?.SetMovementLocked(true);
        Move(direction, speed);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        _movementRecoilCoroutine = null;
        Stop();
        _playerController?.SetMovementLocked(false);
    }
}
