using UnityEngine;

/// <summary>
/// 몬스터 주변을 회전하는 직접 클릭용 약점 표적. 활성·노출·재생성 대기 상태와 표현만 소유하며,
/// 일반 사격 실행이나 자원 소비 정책은 소유하지 않는다
/// (weakpoint-system-design.md Decision 003, Decision 004)
/// </summary>
[DisallowMultipleComponent]
public sealed class WeakPointTarget : MonoBehaviour
{
    [Header("소유자")]
    [Tooltip("이 표적이 중심으로 회전할 소유 몬스터의 Transform")]
    [SerializeField] private Transform _owner;

    [Header("표현")]
    [Tooltip("활성·비차폐 상태에서만 표시할 SpriteRenderer")]
    [SerializeField] private SpriteRenderer _spriteRenderer;
    [Tooltip("직접 클릭 후보 탐색용 Trigger Collider2D. 비노출·대기 중에도 비활성화하지 않고 IsClickable로만 판정한다")]
    [SerializeField] private Collider2D _targetCollider;

    [Header("회전")]
    [Tooltip("소유 몬스터 중심에서 표적까지의 회전 반경(월드 단위)")]
    [SerializeField] private float _rotationRadius = 1f;
    [Tooltip("표적의 회전 각속도(도/초)")]
    [SerializeField] private float _rotationSpeed = 90f;

    [Header("재생성")]
    [Tooltip("직접 클릭 성공 후 다시 활성화되기까지 대기 시간(초)")]
    [SerializeField] private float _cooldownDuration = 3f;

    private float _currentAngleDegrees;
    private float _cooldownRemaining;
    private Player _cachedPlayer;
    private IDamageable _ownerDamageable;

    /// <summary>현재 상태. 테스트·로그 확인용이며 외부는 IsClickable만으로 판단한다</summary>
    public WeakPointState State { get; private set; } = WeakPointState.Inactive;

    /// <summary>지금 직접 클릭으로 발동 가능한지 여부. 플레이어에게 보이는 상태일 때만 true다</summary>
    public bool IsClickable => State == WeakPointState.ActiveVisible;

    /// <summary>소유 몬스터의 IDamageable 참조. 피해 계산·적용은 호출부(PlayerController)가 담당한다</summary>
    public IDamageable OwnerDamageable => _ownerDamageable;

    private void OnEnable()
    {
        ResetRuntimeState();

        if (!HasRequiredReferences())
        {
            Logger.LogError($"{nameof(WeakPointTarget)}: 필수 참조(Owner/SpriteRenderer/Collider2D)가 연결되지 않아 비활성 상태로 유지합니다.", this);
            return;
        }

        _ownerDamageable = _owner.GetComponentInParent<IDamageable>();

        EnterActive();
    }

    private void OnDisable()
    {
        ResetRuntimeState();
    }

    private void LateUpdate()
    {
        if (State == WeakPointState.Cooldown)
        {
            TickCooldown();
            return;
        }

        if (State != WeakPointState.ActiveVisible && State != WeakPointState.ActiveHidden)
        {
            return;
        }

        UpdateRotation();
        UpdateVisibility();
    }

    /// <summary>
    /// 직접 클릭 성공을 표적에 통지해 재생성 대기로 전환한다. 클릭 불가 상태면 아무 효과 없이 false를 반환한다
    /// </summary>
    public bool TryConsumeClick()
    {
        if (!IsClickable)
        {
            return false;
        }

        EnterCooldown();
        return true;
    }

    /// <summary>풀 반환·비활성화 경계에서 회전·대기·표현 상태가 다음 대여로 이어지지 않게 초기화한다</summary>
    public void ResetRuntimeState()
    {
        State = WeakPointState.Inactive;
        _currentAngleDegrees = 0f;
        _cooldownRemaining = 0f;
        _cachedPlayer = null;
        _ownerDamageable = null;
        SetVisible(false);
    }

    private bool HasRequiredReferences()
    {
        return _owner != null && _spriteRenderer != null && _targetCollider != null;
    }

    private void EnterActive()
    {
        State = WeakPointState.ActiveVisible;
        _currentAngleDegrees = 0f;
        UpdateRotation();
        UpdateVisibility();
    }

    private void EnterCooldown()
    {
        State = WeakPointState.Cooldown;
        _cooldownRemaining = _cooldownDuration;
        SetVisible(false);
    }

    private void TickCooldown()
    {
        _cooldownRemaining -= Time.deltaTime;

        if (_cooldownRemaining <= 0f)
        {
            EnterActive();
        }
    }

    private void UpdateRotation()
    {
        _currentAngleDegrees += _rotationSpeed * Time.deltaTime;

        Vector3 direction = DirectionUtility2D.DirectionFromDegrees(_currentAngleDegrees);
        transform.position = _owner.position + direction * _rotationRadius;
    }

    // 노출 조건·시야 거리·벽 차폐는 WeakPointVisionResolver가 단독으로 소유한다.
    // 차폐 기준은 회전하는 약점이 아니라 소유 몬스터 본체여서, 약점이 벽 모서리를 스칠 때 표시가 깜빡이지 않는다
    private void UpdateVisibility()
    {
        bool isVisible = WeakPointVisionResolver.IsVisible(ResolvePlayer(), transform.position, _owner.position);

        State = isVisible ? WeakPointState.ActiveVisible : WeakPointState.ActiveHidden;
        SetVisible(isVisible);
    }

    private void SetVisible(bool isVisible)
    {
        if (_spriteRenderer != null)
        {
            _spriteRenderer.enabled = isVisible;
        }
    }

    // GameManager 등록을 우선 사용하고, 테스트 Scene처럼 GameManager가 없으면 Scene에서 직접 찾는다(호환용 fallback)
    private Player ResolvePlayer()
    {
        if (_cachedPlayer != null)
        {
            return _cachedPlayer;
        }

        _cachedPlayer = GameManager.HasInstance ? GameManager.Instance.GetPlayer() : null;

        if (_cachedPlayer == null)
        {
            _cachedPlayer = FindFirstObjectByType<Player>();
        }

        return _cachedPlayer;
    }
}
