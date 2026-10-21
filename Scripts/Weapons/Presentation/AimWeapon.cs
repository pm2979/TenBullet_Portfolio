using System;
using UnityEngine;

/// <summary>
/// 소유자가 공유하는 무기 조준 회전·좌우 반전·정렬 표현을 적용
/// </summary>
[DisallowMultipleComponent]
public class AimWeapon : MonoBehaviour
{
    private const float FacingFlipThreshold = 0.01f;

    [SerializeField] private Transform _weaponRotationPivot;

    [SerializeField] private SpriteRenderer _weaponSpriteRenderer;

    [Tooltip("최초 조준 이후 목표 각도까지 보간할 회전 속도(도/초)")]
    [SerializeField] private float _rotationDegreesPerSecond = 720f;

    [SerializeField] private bool _isApplySorting = true;

    private const int WeaponBehindOwnerOffset = -1;
    private const int WeaponInFrontOfOwnerOffset = 1;

    private SpriteRenderer _ownerSpriteRenderer;
    private Quaternion _initialPivotLocalRotation;
    private Vector3 _initialPivotLocalScale;
    private int _initialWeaponSortingOrder;
    private bool _hasInitialState;
    private bool _hasLoggedMissingReferences;
    private bool _hasOwnerFacingOverride;
    private bool _hasTargetAimAngle;
    private float _targetAimAngle;

    public bool IsConfigured =>
        _weaponRotationPivot != null && _weaponSpriteRenderer != null && _ownerSpriteRenderer != null;

    /// <summary>
    /// 무기 스프라이트 표시 여부
    /// </summary>
    public bool IsWeaponVisible => _weaponSpriteRenderer != null && _weaponSpriteRenderer.enabled;

    /// <summary>
    /// 조준 회전·반전·정렬 적용 직후 결과 통지
    /// </summary>
    public event Action<WeaponAimData> Aimed;

    private void Awake()
    {
        _ownerSpriteRenderer = GetComponent<SpriteRenderer>();
        CacheInitialStateIfPossible();
    }

    // 재사용 복원을 위해 유효한 최초 표현 상태를 한 번만 보관
    private void CacheInitialStateIfPossible()
    {
        if (_hasInitialState || _weaponRotationPivot == null || _weaponSpriteRenderer == null)
        {
            return;
        }

        _initialPivotLocalRotation = _weaponRotationPivot.localRotation;
        _initialPivotLocalScale = _weaponRotationPivot.localScale;
        _initialWeaponSortingOrder = _weaponSpriteRenderer.sortingOrder;
        _hasInitialState = true;
    }

    /// <summary>
    /// 조준 표현을 적용하고 결과를 한 번 통지
    /// </summary>
    public void Aim(WeaponAimData aimData)
    {
        CacheInitialStateIfPossible();

        if (!IsConfigured)
        {
            if (!_hasLoggedMissingReferences)
            {
                Debug.LogError($"AimWeapon: '{name}'의 Weapon Rotation Pivot, Weapon Sprite Renderer 또는 Owner Sprite Renderer 참조가 없습니다.", this);
                _hasLoggedMissingReferences = true;
            }

            return;
        }

        SetTargetRotation(aimData.AimAngle);
        ApplyFacing(aimData.PlayerDirection.x);
        ApplySorting(aimData.AimDirection);

        Aimed?.Invoke(aimData);
    }

    /// <summary>
    /// 조준 기준점·발사 위치·콜라이더를 유지한 채 무기 스프라이트 표시 전환
    /// </summary>
    public void SetWeaponVisible(bool isVisible)
    {
        if (_weaponSpriteRenderer == null)
        {
            return;
        }

        _weaponSpriteRenderer.enabled = isVisible;
    }

    // 최초 조준은 즉시 적용하고 이후 조준은 보간할 목표 각도로 저장
    private void SetTargetRotation(float aimAngle)
    {
        _targetAimAngle = aimAngle;

        if (!_hasTargetAimAngle)
        {
            _hasTargetAimAngle = true;
            _weaponRotationPivot.eulerAngles = new Vector3(0f, 0f, aimAngle);
        }
    }

    private void Update()
    {
        AdvanceRotation(Time.deltaTime);
    }

    // 전달된 경과 시간만큼 목표 각도로 회전해 프레임과 테스트에서 같은 계산 사용
    private void AdvanceRotation(float deltaTime)
    {
        if (!_hasTargetAimAngle || _weaponRotationPivot == null)
        {
            return;
        }

        float currentAngle = _weaponRotationPivot.eulerAngles.z;
        float nextAngle = Mathf.MoveTowardsAngle(currentAngle, _targetAimAngle, _rotationDegreesPerSecond * deltaTime);
        _weaponRotationPivot.eulerAngles = new Vector3(0f, 0f, nextAngle);
    }

    // 수직 조준에서는 기존 좌우 상태를 유지하고 수평 방향이 분명할 때만 반전
    private void ApplyFacing(float directionX)
    {
        if (_hasOwnerFacingOverride || Mathf.Abs(directionX) < FacingFlipThreshold)
        {
            return;
        }

        ApplyHorizontalFacing(directionX > 0f);
    }

    /// <summary>
    /// 소유자의 좌우 방향을 무기 기준점 반전에 강제로 동기화
    /// </summary>
    public void SetHorizontalFacing(bool isFacingRight)
    {
        CacheInitialStateIfPossible();
        _hasOwnerFacingOverride = true;

        ApplyHorizontalFacing(isFacingRight);
    }

    // 초기 크기의 절댓값을 보존하며 무기 기준점의 수직 축 반전 적용
    private void ApplyHorizontalFacing(bool isFacingRight)
    {
        if (_weaponRotationPivot == null)
        {
            return;
        }

        Vector3 scale = _hasInitialState ? _initialPivotLocalScale : _weaponRotationPivot.localScale;
        float baseScaleY = Mathf.Abs(scale.y);
        scale.y = isFacingRight ? baseScaleY : -baseScaleY;
        _weaponRotationPivot.localScale = scale;
    }

    // 위쪽 조준만 소유자 뒤에 그리고 나머지는 앞에 표시
    private void ApplySorting(AimDirection aimDirection)
    {
        if (!_isApplySorting)
        {
            return;
        }

        _weaponSpriteRenderer.sortingOrder = aimDirection == AimDirection.Up
            ? _ownerSpriteRenderer.sortingOrder + WeaponBehindOwnerOffset
            : _ownerSpriteRenderer.sortingOrder + WeaponInFrontOfOwnerOffset;
    }

    /// <summary>
    /// 재사용 시 이전 조준의 회전·반전·정렬 상태가 남지 않도록 초기 표현 복원
    /// </summary>
    public void ResetRuntimeState()
    {
        _hasLoggedMissingReferences = false;
        _hasOwnerFacingOverride = false;
        _hasTargetAimAngle = false;
        _targetAimAngle = 0f;

        if (!_hasInitialState)
        {
            return;
        }

        _weaponRotationPivot.localRotation = _initialPivotLocalRotation;
        _weaponRotationPivot.localScale = _initialPivotLocalScale;
        _weaponSpriteRenderer.sortingOrder = _initialWeaponSortingOrder;
    }
}
