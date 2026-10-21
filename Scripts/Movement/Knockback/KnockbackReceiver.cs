using System.Collections;
using UnityEngine;

/// <summary>
/// 공격 측이 전달한 방향·속도·지속시간으로 피격 대상의 기존 이동 경계에 넉백을 요청하는 컴포넌트.
/// Enemy는 EnemyMovementController, Player는 PlayerMotor2D/PlayerController로 위임하며
/// 이 컴포넌트 자신은 Rigidbody2D·Transform을 직접 조작하지 않는다.
/// 면역·저항은 Stat이 아닌 이 컴포넌트가 소유하는 Runtime State이며 명시적 초기화만 받는다
/// </summary>
[DisallowMultipleComponent]
public sealed class KnockbackReceiver : MonoBehaviour
{
    [Tooltip("넉백 속도에 곱해지는 저항 배율(0=완전 저항, 1=저항 없음)")]
    [SerializeField, Range(0f, 1f)] private float _resistance = 1f;

    [Tooltip("true면 넉백을 전혀 받지 않는다")]
    [SerializeField] private bool _isImmune = false;

    private PlayerMotor2D _playerMotor;
    private PlayerController _playerController;
    private EnemyMovementController _enemyMovementController;

    private Coroutine _knockbackCoroutine;
    private bool _isKnockedBack;

    /// <summary>
    /// 현재 넉백이 진행 중이면 true. 소유자 이동 호출 전에 설정되므로
    /// 넉백 시작 콜백에서 동기로 발생하는 이동 이벤트에서도 즉시 관찰할 수 있다
    /// </summary>
    public bool IsKnockedBack => _isKnockedBack;

    private void Awake()
    {
        _playerMotor = GetComponent<PlayerMotor2D>();
        _playerController = GetComponent<PlayerController>();
        _enemyMovementController = GetComponent<EnemyMovementController>();
    }

    private void OnDisable()
    {
        ResetRuntimeState();
    }

    /// <summary>
    /// 저항 배율을 설정한다. 소유자 초기화 지점에서 명시적으로 호출한다
    /// </summary>
    public void SetResistance(float resistance)
    {
        _resistance = Mathf.Clamp01(resistance);
    }

    /// <summary>
    /// 넉백 면역 여부를 설정한다. 소유자 초기화 지점에서 명시적으로 호출한다
    /// </summary>
    public void SetImmune(bool isImmune)
    {
        _isImmune = isImmune;
    }

    /// <summary>
    /// 넉백을 요청한다. 면역이거나 방향·속도·지속시간이 유효하지 않으면 무시한다.
    /// 이미 진행 중인 넉백이 있으면 기존 넉백을 끝내고 새 요청으로 덮어쓴다(연속 피격 정책: 덮어쓰기)
    /// </summary>
    public void ApplyKnockback(Vector2 direction, float speed, float duration)
    {
        if (_isImmune || direction.sqrMagnitude <= 0f || speed <= 0f || duration <= 0f)
            return;

        if (_playerMotor == null && _enemyMovementController == null)
        {
            Logger.LogWarning($"{nameof(KnockbackReceiver)}: PlayerMotor2D/EnemyMovementController를 찾지 못해 넉백을 적용할 수 없습니다. ({gameObject.name})");
            return;
        }

        float appliedSpeed = speed * _resistance;
        if (appliedSpeed <= 0f)
            return;

        if (_knockbackCoroutine != null)
        {
            StopCoroutine(_knockbackCoroutine);
            _knockbackCoroutine = null;
            EndKnockbackOnOwner();
        }

        _isKnockedBack = true;
        _knockbackCoroutine = StartCoroutine(KnockbackRoutine(direction.normalized, appliedSpeed, duration));
    }

    /// <summary>
    /// 진행 중인 넉백을 즉시 취소한다(사망·비활성화·풀 반환 등에서 사용). 진행 중이 아니면 아무 효과가 없다
    /// </summary>
    public void CancelKnockback()
    {
        if (_knockbackCoroutine == null)
            return;

        StopCoroutine(_knockbackCoroutine);
        _knockbackCoroutine = null;
        EndKnockbackOnOwner();
        _isKnockedBack = false;
    }

    /// <summary>
    /// Pool 반환 등 재사용 경계에서 진행 상태를 멱등하게 초기화한다
    /// </summary>
    public void ResetRuntimeState()
    {
        CancelKnockback();
    }

    private IEnumerator KnockbackRoutine(Vector2 direction, float speed, float duration)
    {
        StartKnockbackOnOwner(direction, speed);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        EndKnockbackOnOwner();
        _knockbackCoroutine = null;
        _isKnockedBack = false;
    }

    private void StartKnockbackOnOwner(Vector2 direction, float speed)
    {
        if (_enemyMovementController != null)
        {
            _enemyMovementController.ApplyKnockback(direction, speed);
            return;
        }

        if (_playerMotor != null)
        {
            // Dodge는 PositionMotor2D로 별도 진행되므로 넉백 속도와 함께 적용되면 서로 충돌한다.
            // 기존 충돌 취소(OnCollisionEnter2D)와 같은 방식으로 먼저 취소한다.
            // 발사 이동 반동도 같은 이유로 넉백이 우선하므로 먼저 취소한다(weapon-system-design.md Decision 007 Q13)
            _playerMotor.CancelDodge();
            _playerMotor.CancelMovementRecoil();
            _playerController?.SetMovementLocked(true);
            _playerMotor.Move(direction, speed);
        }
    }

    private void EndKnockbackOnOwner()
    {
        if (_enemyMovementController != null)
        {
            _enemyMovementController.EndKnockback();
            return;
        }

        if (_playerMotor != null)
        {
            _playerMotor.Stop();
            _playerController?.SetMovementLocked(false);
        }
    }
}
