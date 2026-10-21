using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 플레이어의 이동·조준·회피 상태를 애니메이션과 이동 효과로 표현한다.
/// </summary>
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(AimWeapon))]
[DisallowMultipleComponent]
public class PlayerAnimationView : MonoBehaviour
{
    private static readonly int MoveXHash = Animator.StringToHash("MoveX");
    private static readonly int MoveYHash = Animator.StringToHash("MoveY");
    private static readonly int AimXHash = Animator.StringToHash("AimX");
    private static readonly int AimYHash = Animator.StringToHash("AimY");
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int DodgeHash = Animator.StringToHash("Dodge");
    private static readonly int IdleStateHash = Animator.StringToHash("Base Layer.Idle");

    // 거의 수직인 방향은 기존 좌우 방향을 유지해 미세한 입력 흔들림을 숨긴다.
    private const float FacingFlipThreshold = 0.01f;

    // 플레이어 중심 근처의 불안정한 조준 입력은 마지막 유효 방향을 유지한다.
    private const float AimDeadZoneRadius = 0.15f;
    private const float AimDeadZoneSqrRadius = AimDeadZoneRadius * AimDeadZoneRadius;

    // 첫 유효 입력 전에는 정면을 기본 방향으로 사용한다.
    private static readonly Vector2 DefaultFacingDirection = new Vector2(0f, -1f);

    [Header("이동 먼지 이펙트")]
    [Tooltip("좌우 이동 시 재생할 Sprite 먼지 이펙트")]
    [FormerlySerializedAs("horizontalDustEffect")]
    [SerializeField] private EffectDefinitionSO _horizontalDustEffect;

    [Tooltip("상하 이동 시 재생할 Sprite 먼지 이펙트")]
    [FormerlySerializedAs("verticalDustEffect")]
    [SerializeField] private EffectDefinitionSO _verticalDustEffect;

    [Tooltip("먼지 이펙트 재생 간격(초)")]
    [FormerlySerializedAs("spawnInterval")]
    [SerializeField] private float _dustSpawnInterval = 1f;

    [Space(10)]
    [Header("이동 사운드")]
    [Tooltip("Player가 일반 이동 중 재생할 사운드. 비워두면 재생하지 않는다")]
    [SerializeField] private SoundDataSO _movementSound;

    private Animator _animator;
    private SpriteRenderer _spriteRenderer;
    private AimWeapon _aimWeapon;
    private PlayerMotor2D _playerMotor2D;
    private KnockbackReceiver _knockbackReceiver;
    private float _dustSpawnTimer;
    private bool _isMoving;

    private Vector2 _lastFacingDirection = DefaultFacingDirection;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _aimWeapon = GetComponent<AimWeapon>();

        // 이동 상태는 루트가 소유하고 이 View는 Visual 자식에 배치된다.
        _playerMotor2D = GetComponentInParent<PlayerMotor2D>();
        _knockbackReceiver = GetComponentInParent<KnockbackReceiver>();
    }

    private void OnEnable()
    {
        _playerMotor2D.MovementChanged += PlayerMotor2D_MovementChanged;
        _playerMotor2D.MovementStopped += PlayerMotor2D_MovementStopped;
        _aimWeapon.Aimed += AimWeapon_Aimed;
        _playerMotor2D.DodgeStarted += PlayerMotor2D_DodgeStarted;
        _playerMotor2D.DodgeEnded += PlayerMotor2D_DodgeEnded;
    }

    private void OnDisable()
    {
        _playerMotor2D.MovementChanged -= PlayerMotor2D_MovementChanged;
        _playerMotor2D.MovementStopped -= PlayerMotor2D_MovementStopped;
        _aimWeapon.Aimed -= AimWeapon_Aimed;
        _playerMotor2D.DodgeStarted -= PlayerMotor2D_DodgeStarted;
        _playerMotor2D.DodgeEnded -= PlayerMotor2D_DodgeEnded;
    }

    // 강제 넉백은 일반 이동으로 표현하지 않고 기존 방향과 효과 간격을 초기화한다.
    private void PlayerMotor2D_MovementChanged(Vector2 direction)
    {
        if (_knockbackReceiver != null && _knockbackReceiver.IsKnockedBack)
        {
            SetIdle();
            _dustSpawnTimer = 0f;
            return;
        }

        SetMovement(direction);
        TryPlayDustEffect(direction);
    }

    private void PlayerMotor2D_MovementStopped()
    {
        SetIdle();
        _dustSpawnTimer = 0f;
    }

    private void AimWeapon_Aimed(WeaponAimData weaponAimData)
    {
        // 데드존 안에서는 마지막 이동 또는 조준 방향을 유지한다.
        if (weaponAimData.PlayerDirection.sqrMagnitude < AimDeadZoneSqrRadius)
            return;

        SetAim(weaponAimData.PlayerDirection);
    }

    private void PlayerMotor2D_DodgeStarted(Vector2 direction, float dodgeDuration)
    {
        PlayDodge(direction);
    }

    // 회피 종료 사유와 관계없이 Trigger를 비워 다음 회피와 섞이지 않게 한다.
    private void PlayerMotor2D_DodgeEnded()
    {
        _animator.ResetTrigger(DodgeHash);
        _animator.Play(IdleStateHash, 0, 0f);
        SetIdle();
    }

    /// <summary>
    /// 이동 방향과 속도를 Animator에 반영한다.
    /// </summary>
    public void SetMovement(Vector2 direction)
    {
        _isMoving = direction.sqrMagnitude > 0f;
        Vector2 facingDirection = ResolveFacingDirection(direction);

        _animator.SetFloat(MoveXHash, facingDirection.x);
        _animator.SetFloat(MoveYHash, facingDirection.y);
        _animator.SetFloat(SpeedHash, direction.sqrMagnitude);

        UpdateFacing(facingDirection);
    }

    /// <summary>
    /// 조준 방향을 Animator와 Sprite 방향에 반영한다.
    /// </summary>
    public void SetAim(Vector2 direction)
    {
        Vector2 facingDirection = ResolveFacingDirection(direction);

        _animator.SetFloat(AimXHash, facingDirection.x);
        _animator.SetFloat(AimYHash, facingDirection.y);

        UpdateFacing(facingDirection);
    }

    /// <summary>
    /// 마지막 방향은 유지한 채 이동 속도만 0으로 만든다.
    /// </summary>
    public void SetIdle()
    {
        _isMoving = false;
        _animator.SetFloat(SpeedHash, 0f);
    }

    /// <summary>
    /// 회피 방향을 반영하고 회피 애니메이션을 시작한다.
    /// </summary>
    public void PlayDodge(Vector2 direction)
    {
        _isMoving = false;
        Vector2 facingDirection = ResolveFacingDirection(direction);

        _animator.SetFloat(MoveXHash, facingDirection.x);
        _animator.SetFloat(MoveYHash, facingDirection.y);

        UpdateFacing(facingDirection);

        _animator.SetTrigger(DodgeHash);
    }

    /// <summary>
    /// 발 접지 Animation Event에서 일반 이동 중일 때만 이동음을 재생한다.
    /// </summary>
    public void NotifyMovementStep()
    {
        bool isKnockedBack = _knockbackReceiver != null && _knockbackReceiver.IsKnockedBack;

        if (!_isMoving || isKnockedBack || _playerMotor2D.IsDodging || _playerMotor2D.IsMovementRecoiling)
        {
            return;
        }

        if (_movementSound == null || !AudioManager.HasInstance)
        {
            return;
        }

        AudioManager.Instance.PlaySfx(_movementSound);
    }

    // 이동의 주축에 맞는 먼지 효과를 재생하며, 풀이나 설정이 없으면 조용히 생략한다.
    private void TryPlayDustEffect(Vector2 moveDirection)
    {
        _dustSpawnTimer -= Time.deltaTime;

        if (_dustSpawnTimer > 0f)
        {
            return;
        }

        _dustSpawnTimer = _dustSpawnInterval;

        if (!PoolManager.HasInstance)
        {
            return;
        }

        bool isHorizontal = Mathf.Abs(moveDirection.x) >= Mathf.Abs(moveDirection.y);
        EffectDefinitionSO dustEffect = isHorizontal ? _horizontalDustEffect : _verticalDustEffect;

        if (dustEffect == null || dustEffect.effectViewPrefab == null)
        {
            return;
        }

        Component rentedComponent = PoolManager.Instance.RentComponent(
            dustEffect.effectViewPrefab,
            _playerMotor2D.transform.position,
            Quaternion.identity);

        EffectView effectView = rentedComponent as EffectView;

        if (effectView == null)
        {
            PoolManager.Instance.ReturnComponent(rentedComponent);
            return;
        }

        bool flipX = isHorizontal && moveDirection.x < 0f;
        bool flipY = !isHorizontal && moveDirection.y < 0f;

        effectView.Play(dustEffect, flipX, flipY);
    }

    // 유효한 방향은 정규화해 기억하고, 0 벡터에는 마지막 방향을 재사용한다.
    private Vector2 ResolveFacingDirection(Vector2 direction)
    {
        if (direction == Vector2.zero)
            return _lastFacingDirection;

        Vector2 normalized = direction.normalized;
        _lastFacingDirection = normalized;
        return normalized;
    }

    // 수평 성분이 충분할 때만 Sprite를 반전해 수직 입력의 기존 좌우 방향을 보존한다.
    private void UpdateFacing(Vector2 direction)
    {
        if (Mathf.Abs(direction.x) < FacingFlipThreshold)
            return;

        _spriteRenderer.flipX = direction.x > 0f;
    }
}
