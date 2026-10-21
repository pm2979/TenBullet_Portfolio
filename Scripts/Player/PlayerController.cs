using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 플레이어 입력을 이동·전투·상호작용 실행 흐름으로 조정한다.
/// </summary>
[RequireComponent(typeof(Player))]
[RequireComponent(typeof(CombatInputReader))]
[RequireComponent(typeof(PlayerInputAdapter))]
[RequireComponent(typeof(PlayerInteractionSensor))]
[DisallowMultipleComponent]
public class PlayerController : MonoBehaviour
{
    [Header("입력")]
    [Tooltip("Player Action Map을 찾을 Input Action Asset. 이 Component가 Map 활성화의 단일 Owner다.")]
    [SerializeField] private InputActionAsset _inputActions;

    [Space(10)]
    [Tooltip("이동 관련 정보 MovementDataSO")]
    [SerializeField] private MovementDataSO _movementData;

    [Space(10)]
    [Header("약점 직접 클릭")]
    [Tooltip("직접 클릭 후보를 탐색할 레이어(Enemy)")]
    [SerializeField] private LayerMask _weakPointLayerMask;

    private Player _player;
    private CombatInputReader _combatInputReader;
    private PlayerInputAdapter _playerInputAdapter;
    private PlayerMotor2D _playerMotor2D;
    private PlayerInteractionSensor _playerInteractionSensor;
    private PlayerParry _playerParry;
    private Stats _stats;
    private bool _wasFireHeldLastFrame = false;
    private float _moveSpeed;
    private float _dodgeAnimationClipLength;

    // Action Map의 활성 수명은 이 컴포넌트만 변경한다.
    private InputActionMap _playerActionMap;
    private bool _loggedMissingPlayerActionMap;

    // 발사 후 감속은 일반 이동에만 적용하고 원본 이동 능력치는 변경하지 않는다.
    private float _firingSlowdownTimer;
    private float _firingSlowdownMultiplier = 1f;

    // 약점 클릭 탐색은 고정 크기 버퍼를 재사용해 입력 프레임의 할당을 피한다.
    private const int MaxWeakPointCandidates = 4;
    private readonly Collider2D[] _weakPointCandidateBuffer = new Collider2D[MaxWeakPointCandidates];
    private ContactFilter2D _weakPointContactFilter;

    // 각 행동 범주는 서로 다른 시스템이 독립적으로 잠글 수 있다.
    private readonly ActionGate _inputGate = new ActionGate();
    private readonly ActionGate _movementGate = new ActionGate();
    private readonly ActionGate _combatGate = new ActionGate();
    private readonly ActionGate _interactionGate = new ActionGate();

    /// <summary>
    /// 현재 회피 이동이 진행 중인지 나타낸다.
    /// </summary>
    public bool IsPlayerRolling => _playerMotor2D.IsDodging;

    /// <summary>
    /// 현재 상호작용 입력을 처리할 수 있는지 나타낸다.
    /// </summary>
    public bool CanInteract => _interactionGate.IsOpen;

    private bool HasActiveWeapon => _player.activeWeapon.GetCurrentWeapon() != null;

    private void Awake()
    {
        _player = GetComponent<Player>();
        _combatInputReader = GetComponent<CombatInputReader>();
        _playerInputAdapter = GetComponent<PlayerInputAdapter>();
        _playerMotor2D = GetComponent<PlayerMotor2D>();
        _playerInteractionSensor = GetComponent<PlayerInteractionSensor>();
        _playerParry = GetComponent<PlayerParry>();
        _stats = GetComponent<Stats>();

        _moveSpeed = _movementData.MoveSpeed;

        // 약점 Collider는 Trigger이므로 전역 물리 설정과 관계없이 클릭 질의에 포함한다.
        _weakPointContactFilter.SetLayerMask(_weakPointLayerMask);
        _weakPointContactFilter.useTriggers = true;

        _playerActionMap = _inputActions != null ? _inputActions.FindActionMap("Player") : null;

        if (_playerActionMap == null && !_loggedMissingPlayerActionMap)
        {
            Logger.LogWarning("PlayerController에 Input Action Asset 또는 Player Action Map이 연결되지 않아 입력을 활성화할 수 없습니다.", this);
            _loggedMissingPlayerActionMap = true;
        }
    }

    private void OnEnable()
    {
        _playerMotor2D.DodgeStarted += PlayerMotor2D_DodgeStarted;
        _playerMotor2D.DodgeEnded += PlayerMotor2D_DodgeEnded;

        _player.aimWeapon.SetWeaponVisible(!_playerMotor2D.IsDodging);
        _playerActionMap?.Enable();
    }

    private void OnDisable()
    {
        // 구독 해제 전에 회피를 취소해 종료 처리로 애니메이션과 무적을 복원한다.
        _playerMotor2D.CancelDodge();
        _playerMotor2D.Stop();
        _playerParry?.Cancel();
        _player.aimWeapon.SetWeaponVisible(true);

        _playerMotor2D.DodgeStarted -= PlayerMotor2D_DodgeStarted;
        _playerMotor2D.DodgeEnded -= PlayerMotor2D_DodgeEnded;

        _playerActionMap?.Disable();

        ResetFiringSlowdown();
    }

    private void Start()
    {
        SetPlayerAnimationSpeed();

        CacheDodgeAnimationClipLength();

    }

    // 회피의 확정 지속 시간에 애니메이션 속도와 전용 무적 시간을 맞춘다.
    private void PlayerMotor2D_DodgeStarted(Vector2 direction, float dodgeDuration)
    {
        _player.aimWeapon.SetWeaponVisible(false);

        if (dodgeDuration > 0f && _dodgeAnimationClipLength > 0f)
        {
            _player.animator.speed = _dodgeAnimationClipLength / dodgeDuration;
        }

        _player.hitImmunity.StartDodgeInvulnerability(_playerMotor2D.ActiveDodgeInvulnerabilityDuration);
    }

    // 모든 회피 종료 경로에서 전용 무적과 표현 상태를 일반 이동 기준으로 복원한다.
    private void PlayerMotor2D_DodgeEnded()
    {
        _player.hitImmunity.CancelDodgeInvulnerability();
        _player.aimWeapon.SetWeaponVisible(true);
        SetPlayerAnimationSpeed();
    }

    // 첫 회피 Clip 길이를 저장해 회피 지속 시간에 맞는 재생 속도를 계산한다.
    private void CacheDodgeAnimationClipLength()
    {
        AnimationClip[] clips = _player.animator.runtimeAnimatorController.animationClips;

#if UNITY_EDITOR
        float? firstLength = null;

        foreach (AnimationClip clip in clips)
        {
            if (!clip.name.Contains("Dodge")) continue;

            if (firstLength == null)
            {
                firstLength = clip.length;
            }
            else if (!Mathf.Approximately(firstLength.Value, clip.length))
            {
                Logger.LogWarning($"구르기 애니메이션 클립들의 길이가 서로 달라 고정 지속 시간 재생 속도가 근사치가 됩니다: {clip.name} = {clip.length}");
            }
        }
#endif

        foreach (AnimationClip clip in clips)
        {
            if (!clip.name.Contains("Dodge")) continue;

            _dodgeAnimationClipLength = clip.length;
            break;
        }
    }

    // 현재 이동 속도에 비례하도록 일반 애니메이션 재생 속도를 맞춘다.
    private void SetPlayerAnimationSpeed()
    {
        _player.animator.speed = CurrentMoveSpeed() / CombatSettings.baseSpeedForPlayerAnimations;
    }

    // 런타임 능력치가 없으면 이동 설정값을 사용하고, 발사 감속은 계산 결과에만 적용한다.
    private float CurrentMoveSpeed()
    {
        float baseMoveSpeed;

        if (_stats != null && _stats.RuntimeStats != null)
        {
            baseMoveSpeed = _stats.RuntimeStats.GetFinalValue(StatType.MoveSpeed);
        }
        else
        {
            baseMoveSpeed = _moveSpeed;
        }

        return baseMoveSpeed * CurrentFiringSlowdownMultiplier();
    }

    private float CurrentFiringSlowdownMultiplier()
    {
        return _firingSlowdownTimer > 0f ? _firingSlowdownMultiplier : 1f;
    }

    /// <summary>
    /// 발사 후 이동 감속을 시작하며, 재요청 시 배율과 남은 시간을 새 값으로 교체한다.
    /// </summary>
    public void StartFiringSlowdown(float multiplier, float duration)
    {
        if (duration <= 0f || Mathf.Approximately(multiplier, 1f))
        {
            return;
        }

        _firingSlowdownMultiplier = multiplier;
        _firingSlowdownTimer = duration;
    }

    private void ResetFiringSlowdown()
    {
        _firingSlowdownTimer = 0f;
        _firingSlowdownMultiplier = 1f;
    }

    private void TickFiringSlowdown(float deltaTime)
    {
        if (_firingSlowdownTimer <= 0f) return;

        _firingSlowdownTimer -= deltaTime;

        if (_firingSlowdownTimer <= 0f)
        {
            _firingSlowdownTimer = 0f;
            _firingSlowdownMultiplier = 1f;
        }
    }

    private void Update()
    {
        // 회피 중에도 감속 지속 시간이 흐르도록 입력 조기 종료보다 먼저 갱신한다.
        TickFiringSlowdown(Time.deltaTime);

        // 회피 중에는 전투 입력 세션을 끊어 종료 직후 이전 Hold 입력이 이어지지 않게 한다.
        if (_playerMotor2D.IsDodging)
        {
            _wasFireHeldLastFrame = false;
            return;
        }

        // 닫힌 전투 게이트는 Hold 상태도 끊어 재개 프레임을 새 발사 입력으로 처리한다.
        bool combatInputOpen = _inputGate.IsOpen && _combatGate.IsOpen;
        if (!combatInputOpen)
        {
            _wasFireHeldLastFrame = false;
        }

        if (!_inputGate.IsOpen) return;

        // 패링 중에는 회피 전환만 허용하고 발사 Hold 세션은 매 프레임 끊는다.
        if (_playerParry != null && _playerParry.IsActive)
        {
            _wasFireHeldLastFrame = false;
            ParryActiveInput();
            return;
        }

        // 패링 입력은 같은 프레임의 이동과 전투보다 우선한다.
        if (_combatInputReader.ParryPressed && TryStartParry())
        {
            _wasFireHeldLastFrame = false;
            return;
        }

        if (_movementGate.IsOpen)
        {
            MovementInput();

            // 회피 시작에 성공한 프레임에는 이후 전투 입력을 처리하지 않는다.
            if (_playerMotor2D.IsDodging)
            {
                _wasFireHeldLastFrame = false;
                return;
            }
        }

        if (combatInputOpen)
        {
            WeaponInput();
        }

        if (_interactionGate.IsOpen)
        {
            UseItemInput();
        }
    }

    // 방향 입력을 일반 이동 또는 회피 시작으로 전달하고, 입력이 없으면 이동을 멈춘다.
    private void MovementInput()
    {
        Vector2 direction = _combatInputReader.Move;
        bool dodgePressed = _combatInputReader.DodgePressed;

        if (direction != Vector2.zero)
        {
            if (!dodgePressed)
            {
                _playerMotor2D.MoveWithCollisionCheck(direction, CurrentMoveSpeed());
            }
            else if (dodgePressed)
            {
                TryStartDodge(direction);
            }

        }
        else
        {
            _playerMotor2D.Stop();
        }
    }

    // 회피 조건을 먼저 확정하고 스태미나 소비 직후 동일한 수치로 회피를 시작한다.
    private bool TryStartDodge(Vector2 direction)
    {
        if (!StatTypeRules.IsValidBaseValue(StatType.DodgeDistance, _movementData.DodgeDistance) ||
            !StatTypeRules.IsValidBaseValue(StatType.DodgeStaminaCost, _movementData.DodgeStaminaCost) ||
            !StatTypeRules.IsValidBaseValue(StatType.DodgeInvulnerabilityDuration, _movementData.DodgeInvulnerabilityDuration) ||
            !float.IsFinite(_movementData.DodgeDuration) || _movementData.DodgeDuration <= 0f)
        {
            return false;
        }

        float finalDistance = ResolveFinalStat(StatType.DodgeDistance, _movementData.DodgeDistance);
        float finalStaminaCost = ResolveFinalStat(StatType.DodgeStaminaCost, _movementData.DodgeStaminaCost);
        float fixedDodgeDuration = _movementData.DodgeDuration;
        float finalInvulnerabilityDuration = Mathf.Min(
            ResolveFinalStat(StatType.DodgeInvulnerabilityDuration, _movementData.DodgeInvulnerabilityDuration),
            fixedDodgeDuration);

        if (!_playerMotor2D.CanStartDodge(direction, finalDistance, fixedDodgeDuration, finalInvulnerabilityDuration))
        {
            return false;
        }

        if (!TryConsumeDodgeStamina(finalStaminaCost))
        {
            return false;
        }

        // 소비와 시작 사이에 수치를 다시 계산하지 않아 판정과 실행이 어긋나지 않게 한다.
        return _playerMotor2D.TryStartDodge(
            direction,
            finalDistance,
            fixedDodgeDuration,
            finalInvulnerabilityDuration);
    }

    // 방향과 회피 입력이 함께 들어오면 패링을 종료하고 기존 회피 시작 흐름으로 전환한다.
    private void ParryActiveInput()
    {
        if (!_movementGate.IsOpen) return;

        Vector2 direction = _combatInputReader.Move;

        if (direction == Vector2.zero || !_combatInputReader.DodgePressed) return;

        _playerParry.Cancel();
        TryStartDodge(direction);
    }

    // 패링 조건과 최종 수치를 확인한 뒤 비용 소비에 성공한 경우에만 판정을 활성화한다.
    private bool TryStartParry()
    {
        if (_playerParry == null || _playerParry.IsActive) return false;

        ParryConfigSO parryConfig = _playerParry.Config;

        if (parryConfig == null) return false;

        if (_player.stamina == null || !_player.stamina.IsInitialized) return false;

        if (_playerMotor2D.IsDodging || _playerMotor2D.IsMovementRecoiling) return false;

        if (_player.knockbackReceiver != null && _player.knockbackReceiver.IsKnockedBack) return false;

        if (!StatTypeRules.IsValidBaseValue(StatType.ParryWindow, parryConfig.ParryWindow) ||
            !StatTypeRules.IsValidBaseValue(StatType.ParryStaminaCost, parryConfig.StaminaCost))
        {
            return false;
        }

        float finalWindow = ResolveFinalStat(StatType.ParryWindow, parryConfig.ParryWindow);
        float finalStaminaCost = ResolveFinalStat(StatType.ParryStaminaCost, parryConfig.StaminaCost);

        if (!_playerParry.CanStart(finalWindow)) return false;

        if (!_player.stamina.TryConsume(finalStaminaCost)) return false;

        // 소비에 사용한 수치와 같은 시점에 확정한 판정 시간을 그대로 사용한다.
        _playerParry.TryStart(finalWindow);

        // 패링 중 직전 이동 의도와 속도가 남지 않게 한다.
        _playerMotor2D.Stop();
        return true;
    }

    // 시작 시점의 런타임 보정값을 적용하며, 능력치가 없으면 설정 기본값을 유지한다.
    private float ResolveFinalStat(StatType statType, float baseValue)
    {
        if (_stats != null && _stats.RuntimeStats != null)
        {
            return _stats.RuntimeStats.ApplyModifiers(statType, baseValue);
        }

        return baseValue;
    }

    // 회피 비용을 최종 수치로 계산해 소비하며, 스태미나가 없는 기존 구성은 허용한다.
    private bool TryConsumeDodgeStamina()
    {
        if (!StatTypeRules.IsValidBaseValue(StatType.DodgeStaminaCost, _movementData.DodgeStaminaCost))
        {
            return false;
        }

        float finalStaminaCost = ResolveFinalStat(StatType.DodgeStaminaCost, _movementData.DodgeStaminaCost);
        return TryConsumeDodgeStamina(finalStaminaCost);
    }

    private bool TryConsumeDodgeStamina(float finalStaminaCost)
    {
        if (_player.stamina == null) return true;

        return _player.stamina.TryConsume(finalStaminaCost);
    }

    // 하나의 조준 결과를 조준 표현과 발사 요청에 함께 사용한다.
    private void WeaponInput()
    {
        Vector3 weaponDirection;
        Vector2 playerDirection;
        float weaponAngleDegrees, playerAngleDegrees;
        AimDirection playerAimDirection;

        AimWeaponInput(out weaponDirection, out playerDirection, out weaponAngleDegrees, out playerAngleDegrees, out playerAimDirection);

        FireWeaponInput(weaponDirection, playerDirection, weaponAngleDegrees, playerAngleDegrees, playerAimDirection);
    }

    private void AimWeaponInput(out Vector3 weaponDirection, out Vector2 playerDirection, out float weaponAngleDegrees, out float playerAngleDegrees, out AimDirection playerAimDirection)
    {
        AimResult aimResult = _playerInputAdapter.ResolveAim(transform.position, _player.activeWeapon.GetShootPosition());

        weaponDirection = aimResult.WeaponDirection;
        playerDirection = aimResult.PlayerDirection;
        weaponAngleDegrees = aimResult.WeaponAngleDegrees;
        playerAngleDegrees = aimResult.PlayerAngleDegrees;
        playerAimDirection = aimResult.AimDirection;

        WeaponAimData weaponAimData = new WeaponAimData(playerAimDirection, playerAngleDegrees, weaponAngleDegrees, weaponDirection, playerDirection);
        _player.aimWeapon.Aim(weaponAimData);
    }

    private void FireWeaponInput(Vector3 weaponDirection, Vector2 playerDirection, float weaponAngleDegrees, float playerAngleDegrees, AimDirection playerAimDirection)
    {
        if (!HasActiveWeapon)
        {
            _wasFireHeldLastFrame = false;
            return;
        }

        if (_combatInputReader.FireHeld)
        {
            bool isFirstPressFrame = !_wasFireHeldLastFrame;
            WeaponAimData weaponAimData = new WeaponAimData(playerAimDirection, playerAngleDegrees, weaponAngleDegrees, weaponDirection, playerDirection);

            // 첫 Hold 프레임의 약점 클릭이 성공하면 같은 입력으로 일반 투사체를 발사하지 않는다.
            if (isFirstPressFrame && TryHandleWeakPointClick(weaponAimData))
            {
                _wasFireHeldLastFrame = true;
                return;
            }

            _player.fireWeapon.RequestFire(new FireWeaponRequest(true, _wasFireHeldLastFrame, weaponAimData));
            _wasFireHeldLastFrame = true;
        }
        else
        {
            _wasFireHeldLastFrame = false;
        }
    }

    // 조준 위치를 변환할 수 없거나 약점이 없으면 일반 사격 흐름을 계속하도록 false를 반환한다.
    private bool TryHandleWeakPointClick(WeaponAimData weaponAimData)
    {
        if (!_playerInputAdapter.TryGetAimWorldPosition(transform.position, out Vector2 aimWorldPosition))
        {
            return false;
        }

        return TryConsumeWeakPointAt(aimWorldPosition, weaponAimData);
    }

    // 클릭 가능한 약점과 피해 대상을 먼저 검증한 뒤 비용·피해·표적 소비·Hit 이벤트를 순서대로 확정한다.
    private bool TryConsumeWeakPointAt(Vector2 worldPosition, WeaponAimData weaponAimData)
    {
        int candidateCount = Physics2D.OverlapPoint(worldPosition, _weakPointContactFilter, _weakPointCandidateBuffer);

        for (int i = 0; i < candidateCount; i++)
        {
            WeakPointTarget candidate = _weakPointCandidateBuffer[i].GetComponent<WeakPointTarget>();

            // 잘못 구성된 후보는 발사 비용이나 표적 상태를 바꾸기 전에 제외한다.
            if (candidate == null || !candidate.IsClickable || candidate.OwnerDamageable == null)
            {
                continue;
            }

            if (!_player.fireWeapon.TryFireWithoutProjectile(weaponAimData, out int shotId))
            {
                continue;
            }

            Weapon weapon = _player.activeWeapon.GetCurrentWeapon();
            CombatResult combatResult = ApplyWeakPointDamage(candidate, weapon);
            candidate.TryConsumeClick();
            weapon.TryPublishHit(new WeaponHitEvent(WeaponHitKind.WeakPoint, combatResult, shotId));
            return true;
        }

        return false;
    }

    // 직접 클릭 피해에는 약점 배율만 적용하고 실제 피해 결과를 전투 결과로 묶는다.
    private CombatResult ApplyWeakPointDamage(WeakPointTarget candidate, Weapon weapon)
    {
        IDamageable ownerDamageable = candidate.OwnerDamageable;

        int baseDamage = weapon.weaponDetails.weaponDamage;
        float multiplier = _player.stats != null && _player.stats.RuntimeStats != null
            ? _player.stats.RuntimeStats.ApplyModifiers(StatType.WeakPointDamageMultiplier, 1.5f)
            : 1.5f;
        int finalDamage = Mathf.Max(1, Mathf.RoundToInt(baseDamage * multiplier));

        DamageResult damageResult = ownerDamageable.TryTakeDamage(new DamageInfo(_player.health.Faction, _player.health, finalDamage));

        // 피격 점멸은 체력 변경 구독자가 처리하므로 여기서는 성공한 피해의 넉백만 추가한다.
        if (damageResult.WasApplied)
        {
            ApplyWeakPointKnockback(candidate, weapon);
        }

        CombatResult combatResult = new CombatResult(
            ownerDamageable, weapon, finalDamage, damageResult, isCritical: false, hitPosition: candidate.transform.position);

        PlayWeakPointHitEffect(weapon, candidate.transform.position);

        return combatResult;
    }

    // 투사체가 없는 직접 클릭도 몬스터 본체 방향을 기준으로 동일한 넉백 수치를 적용한다.
    private void ApplyWeakPointKnockback(WeakPointTarget candidate, Weapon weapon)
    {
        float knockbackSpeed = weapon.weaponDetails.weaponKnockbackSpeed;
        float knockbackDuration = weapon.weaponDetails.weaponKnockbackDuration;

        if (knockbackSpeed <= 0f || knockbackDuration <= 0f)
        {
            return;
        }

        KnockbackReceiver receiver = candidate.GetComponentInParent<KnockbackReceiver>();

        if (receiver == null)
        {
            return;
        }

        Vector2 direction = (Vector2)receiver.transform.position - (Vector2)transform.position;

        receiver.ApplyKnockback(direction.normalized, knockbackSpeed, knockbackDuration);
    }

    // 약점 위치에서 피격 효과를 대여하며, 풀이나 설정이 없으면 조용히 생략한다.
    private void PlayWeakPointHitEffect(Weapon weapon, Vector2 hitPosition)
    {
        EffectDefinitionSO projectileHitSpriteEffect = weapon.weaponDetails.projectileDefinition?.projectileHitSpriteEffect;

        if (projectileHitSpriteEffect == null || projectileHitSpriteEffect.effectViewPrefab == null)
        {
            return;
        }

        if (!PoolManager.HasInstance)
        {
            return;
        }

        Component rentedComponent = PoolManager.Instance.RentComponent(projectileHitSpriteEffect.effectViewPrefab, hitPosition, Quaternion.identity);

        EffectView effectView = rentedComponent as EffectView;

        if (effectView == null)
        {
            PoolManager.Instance.ReturnComponent(rentedComponent);
            return;
        }

        effectView.Play(projectileHitSpriteEffect);
    }

    // 사용 입력을 가장 가까운 상호작용 대상 탐색에 전달한다.
    private void UseItemInput()
    {
        if (_combatInputReader.UseItemPressed)
        {
            _playerInteractionSensor.TryUseNearestInteractable();
        }
    }

    /// <summary>
    /// 이동 입력만 중첩 안전하게 잠그거나 해제한다.
    /// </summary>
    public void SetMovementLocked(bool isLocked)
    {
        if (isLocked)
        {
            _movementGate.Block();
        }
        else
        {
            _movementGate.Unblock();
        }
    }

    /// <summary>
    /// 플레이어의 모든 행동 게이트에서 차단 요청 하나씩을 해제한다.
    /// </summary>
    public void EnablePlayer()
    {
        _inputGate.Unblock();
        _movementGate.Unblock();
        _combatGate.Unblock();
        _interactionGate.Unblock();
    }

    /// <summary>
    /// 모든 행동을 차단하고 진행 중인 강제 이동과 감속 상태를 정리한다.
    /// </summary>
    public void DisablePlayer()
    {
        _inputGate.Block();
        _movementGate.Block();
        _combatGate.Block();
        _interactionGate.Block();

        _playerMotor2D.CancelDodge();
        _playerMotor2D.CancelMovementRecoil();
        _playerMotor2D.Stop();

        ResetFiringSlowdown();
    }
}
