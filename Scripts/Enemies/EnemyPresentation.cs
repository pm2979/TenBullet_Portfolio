using System;
using UnityEngine;

/// <summary>Enemy 수명·행동·공격·이동 상태를 Animator와 Visual Root 표현으로 변환</summary>
// 게임 규칙을 결정하지 않고 Controller 이벤트 소비와 Animation Event 전달만 담당
[DisallowMultipleComponent]
public sealed class EnemyPresentation : MonoBehaviour
{
    private static readonly int MoveXHash = Animator.StringToHash("MoveX");
    private static readonly int MoveYHash = Animator.StringToHash("MoveY");
    private static readonly int AimXHash = Animator.StringToHash("AimX");
    private static readonly int AimYHash = Animator.StringToHash("AimY");
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int DeathHash = Animator.StringToHash("Death");

    [Tooltip("실제 Parameter·Controller가 아직 확정되지 않았으므로 비워 둘 수 있다")]
    [SerializeField] private Animator _animator;

    [Tooltip("좌우 반전을 트리거할 최소 X 방향 크기. 값을 키우면 타겟이 수직 방향 근처에 있을 때 좌우 반전이 반복되지 않는다")]
    [SerializeField] private float _facingFlipDeadZone = 0.2f;

    [Tooltip("좌우 반전을 적용할 Sprite 전용 자식 Transform. 게임플레이 Root/Collider는 반전하지 않으므로 비워 두면 반전을 적용하지 않는다")]
    [SerializeField] private Transform _visualRoot;

    [Tooltip("회전 가능한 무기가 있는 Enemy만 연결한다. 무기 없는 Enemy는 비워 둔다")]
    [SerializeField] private AimWeapon _aimWeapon;

    [Space(10)]
    [Header("방향·사망 연출")]
    [Tooltip("Visual 원본 Sprite가 오른쪽을 바라보는지 여부. false면 좌우 반전 기준을 반대로 적용한다")]
    [SerializeField] private bool _visualFacesRight = true;

    [Tooltip("Dying 진입 시 Death Trigger를 Animator에 전달할지 여부. Death 파라미터가 없는 기존 Controller는 false로 유지한다")]
    [SerializeField] private bool _useDeathTrigger;

    [Space(10)]
    [Header("연출 완료 대기")]
    [Tooltip("Spawn 연출 완료 Animation Event(SpawnCompleted)를 실제로 기다릴지 여부. false면 Initialize 직후 바로 Active로 전환한다")]
    [SerializeField] private bool _waitForSpawnCompletion;

    [Tooltip("사망 연출 완료 Animation Event(DeathCompleted)를 실제로 기다릴지 여부. false면 사망 확정 즉시 Pool 반환을 요청한다")]
    [SerializeField] private bool _waitForDeathCompletion;

    private EnemyAttackController _attackController;
    private EnemyMovementController _movementController;
    private readonly AimResolver _aimResolver = new AimResolver();

    private float _visualRootBaseScaleX = 1f;
    private bool _hasFacing;
    private bool _isFacingRight;
    private bool _canAimWeapon;
    private bool _hasWeaponAimDirection;
    private Vector2 _lastWeaponAimDirection;
    private bool _hasTriggeredDeath;

    /// <summary>Actor가 Spawn 완료 신호를 기다려야 하는지 여부</summary>
    public bool WaitForSpawnCompletion => _waitForSpawnCompletion;

    /// <summary>Actor가 사망 연출 완료 신호를 기다려야 하는지 여부</summary>
    public bool WaitForDeathCompletion => _waitForDeathCompletion;

    /// <summary>Spawn 연출 완료를 Actor에 전달하는 이벤트</summary>
    public event Action SpawnCompleted;

    /// <summary>사망 연출 완료를 Actor에 전달하는 이벤트</summary>
    public event Action DeathCompleted;

    private void Awake()
    {
        _attackController = GetComponent<EnemyAttackController>();
        _movementController = GetComponent<EnemyMovementController>();

        if (_visualRoot != null)
        {
            float baseScaleX = Mathf.Abs(_visualRoot.localScale.x);
            _visualRootBaseScaleX = baseScaleX > 0f ? baseScaleX : 1f;
        }
    }

    private void OnEnable()
    {
        if (_attackController != null)
        {
            _attackController.WeaponAimChanged += HandleWeaponAimChanged;
        }

        if (_movementController != null)
        {
            _movementController.MovementChanged += HandleMovementChanged;
            _movementController.MovementStopped += HandleMovementStopped;
            _movementController.FacingChanged += HandleFacingChanged;
        }
    }

    private void OnDisable()
    {
        if (_attackController != null)
        {
            _attackController.WeaponAimChanged -= HandleWeaponAimChanged;
        }

        if (_movementController != null)
        {
            _movementController.MovementChanged -= HandleMovementChanged;
            _movementController.MovementStopped -= HandleMovementStopped;
            _movementController.FacingChanged -= HandleFacingChanged;
        }

        ResetRuntimeState();
    }

    /// <summary>Actor 수명 상태를 Animator 표현에 반영</summary>
    public void OnLifecycleChanged(EnemyLifecycleState state)
    {
        _canAimWeapon = state == EnemyLifecycleState.Active;

        if (_canAimWeapon)
        {
            _hasWeaponAimDirection = false;
        }

        if (state == EnemyLifecycleState.Dying && _useDeathTrigger && _animator != null && !_hasTriggeredDeath)
        {
            _animator.SetTrigger(DeathHash);
            _hasTriggeredDeath = true;
        }
    }

    /// <summary>Decision 행동 상태를 Animator 표현에 반영</summary>
    public void OnBehaviorChanged(EnemyBehaviorState state)
    {
    }

    /// <summary>공격 단계를 Animator 표현에 반영</summary>
    public void OnAttackPhaseChanged(EnemyAttackPhase phase)
    {
    }

    /// <summary>피해 발생을 피격 Trigger로 변환</summary>
    public void OnDamaged(HealthChange change)
    {
    }

    /// <summary>Spawn 연출 종료 Animation Event 진입점</summary>
    public void NotifySpawnCompleted()
    {
        SpawnCompleted?.Invoke();
    }

    /// <summary>공격 단계 전환 Animation Marker를 Attack Controller에 전달</summary>
    public void NotifyAttackMarker()
    {
        _attackController?.NotifyAnimationMarker();
    }

    /// <summary>실제 공격 출력 Animation Marker를 현재 Action에 전달</summary>
    public void NotifyAttackActionMarker(int markerId)
    {
        _attackController?.NotifyAttackActionMarker(markerId);
    }

    /// <summary>사망 연출 종료 Animation Event 진입점</summary>
    public void NotifyDeathCompleted()
    {
        DeathCompleted?.Invoke();
    }

    // 일반 이동만 Animator 방향과 속도로 반영하고 넉백은 Idle 표현 유지
    private void HandleMovementChanged(Vector2 direction)
    {
        if (_movementController != null && _movementController.IsKnockedBack)
        {
            if (_animator != null)
            {
                _animator.SetFloat(SpeedHash, 0f);
            }

            return;
        }

        Vector2 normalizedDirection = direction.sqrMagnitude > 0f ? direction.normalized : Vector2.zero;

        if (_animator != null)
        {
            _animator.SetFloat(MoveXHash, normalizedDirection.x);
            _animator.SetFloat(MoveYHash, normalizedDirection.y);
            _animator.SetFloat(SpeedHash, 1f);
        }
    }

    // 정지 시 마지막 방향을 보존하고 Animator 속도만 0으로 전환
    private void HandleMovementStopped()
    {
        if (_animator != null)
        {
            _animator.SetFloat(SpeedHash, 0f);
        }
    }

    // 물리 이동과 독립된 바라보기 방향을 반영하되 넉백 중에는 기존 방향 유지
    private void HandleFacingChanged(Vector2 direction)
    {
        if (_movementController != null && _movementController.IsKnockedBack)
        {
            return;
        }

        if (_attackController != null && _attackController.IsTargetTransformLocked)
        {
            return;
        }

        Vector2 normalizedDirection = direction.sqrMagnitude > 0f ? direction.normalized : Vector2.zero;

        if (_animator != null)
        {
            _animator.SetFloat(AimXHash, normalizedDirection.x);
            _animator.SetFloat(AimYHash, normalizedDirection.y);
        }

        UpdateFacing(normalizedDirection.x);

        // 공격 외 상태에서는 몸 방향과 Weapon 방향을 함께 갱신해 마지막 공격 각도 잔존 방지
        bool isAttacking = _attackController != null && _attackController.CanUpdateWeaponAim;

        if (_canAimWeapon && _aimWeapon != null && !isAttacking)
        {
            ApplyWeaponAim(normalizedDirection);
        }
    }

    private void HandleWeaponAimChanged(Vector2 direction)
    {
        if (!_canAimWeapon || _aimWeapon == null)
        {
            return;
        }

        ApplyWeaponAim(direction);
    }

    private void ApplyWeaponAim(Vector2 direction)
    {
        Vector2 normalizedDirection = direction.normalized;

        if (_hasWeaponAimDirection && normalizedDirection == _lastWeaponAimDirection)
        {
            return;
        }

        AimResult result = _aimResolver.ResolveDirection(normalizedDirection);
        WeaponAimData aimData = new WeaponAimData(
            result.AimDirection,
            result.PlayerAngleDegrees,
            result.WeaponAngleDegrees,
            result.WeaponDirection,
            result.PlayerDirection);

        _aimWeapon.Aim(aimData);
        _lastWeaponAimDirection = normalizedDirection;
        _hasWeaponAimDirection = true;
    }

    // 미세한 X 입력은 무시하고 Visual Root의 기준 스케일 부호만 변경
    private void UpdateFacing(float directionX)
    {
        if (_visualRoot == null || Mathf.Abs(directionX) < _facingFlipDeadZone)
        {
            return;
        }

        bool isFacingRight = directionX > 0f;

        if (_hasFacing && isFacingRight == _isFacingRight)
        {
            return;
        }

        _hasFacing = true;
        _isFacingRight = isFacingRight;

        _aimWeapon?.SetHorizontalFacing(isFacingRight);

        bool usePositiveScaleX = _visualFacesRight ? isFacingRight : !isFacingRight;
        Vector3 scale = _visualRoot.localScale;
        scale.x = usePositiveScaleX ? _visualRootBaseScaleX : -_visualRootBaseScaleX;
        _visualRoot.localScale = scale;
    }

    /// <summary>Animator·Visual Root·외부 이벤트를 Pool 재사용 전 상태로 초기화</summary>
    public void ResetRuntimeState()
    {
        if (_animator != null)
        {
            _animator.Rebind();

            if (_animator.isActiveAndEnabled)
            {
                _animator.Update(0f);
            }
        }

        if (_visualRoot != null && _hasFacing)
        {
            Vector3 scale = _visualRoot.localScale;
            scale.x = _visualRootBaseScaleX;
            _visualRoot.localScale = scale;
        }

        _hasFacing = false;
        _isFacingRight = false;
        _canAimWeapon = false;
        _hasWeaponAimDirection = false;
        _lastWeaponAimDirection = Vector2.zero;
        _hasTriggeredDeath = false;

        _aimWeapon?.ResetRuntimeState();

        SpawnCompleted = null;
        DeathCompleted = null;
    }
}
