using System;
using UnityEngine;

/// <summary>Enemy 구성 요소를 조립하고 Spawn·Active·Death·Pool 반환 수명주기를 조정</summary>
// 판단·이동·공격 세부 규칙은 각 Controller에 위임하고 반환은 이벤트로만 요청
[RequireComponent(typeof(Health))]
[RequireComponent(typeof(Stats))]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(EnemyTargetSensor))]
[RequireComponent(typeof(EnemyDecisionController))]
[RequireComponent(typeof(EnemyMovementController))]
[RequireComponent(typeof(EnemyAttackController))]
[DisallowMultipleComponent]
public sealed class EnemyActor : MonoBehaviour
{
    [Tooltip("Spawn 연출 완료 Animation Event(EnemyPresentation.SpawnCompleted)가 없을 때 Active로 강제 전환할 대기 시간(초). " +
             "0 이하이면 자동 전환하지 않고 신호만 기다린다. 실제 콘텐츠 값은 아직 정해지지 않았다")]
    [SerializeField] private float _spawnCompletionSafeTimeout = 0f;

    [Tooltip("사망 연출 완료 Animation Event(EnemyPresentation.DeathCompleted)가 없을 때 Pool 반환을 요청할 대기 시간(초). " +
             "0 이하이면 자동 전환하지 않고 신호만 기다린다. 실제 콘텐츠 값은 아직 정해지지 않았다")]
    [SerializeField] private float _deathCompletionSafeTimeout = 0f;

    [Tooltip("피격 플래시를 재생할 Visual SpriteRenderer. 자식 GameObject에 있어 GetComponent로 찾을 수 없으므로 Inspector로 연결한다")]
    [SerializeField] private SpriteRenderer _spriteRenderer;

    private Health _health;
    private Shield _shield;
    private KnockbackReceiver _knockbackReceiver;
    private Rigidbody2D _rigidbody2D;
    private Stats _stats;
    private EnemyTargetSensor _targetSensor;
    private EnemyDecisionController _decisionController;
    private EnemyMovementController _movementController;
    private EnemyAttackController _attackController;
    private BossPhaseController _bossPhaseController;
    private EnemyPresentation _presentation;
    private HitFeedback _hitFeedback;
    private Collider2D[] _gameplayColliders;
    private bool[] _lifecycleColliderStates;

    private bool _hasConfirmedDeath;
    private float _spawnElapsed;
    private float _deathElapsed;

    public EnemyLifecycleState LifecycleState { get; private set; } = EnemyLifecycleState.Inactive;
    public EnemyArchetypeSO Archetype { get; private set; }
    public EnemyRuntimeConfig RuntimeConfig { get; private set; }

    /// <summary>체력 소진으로 사망이 확정된 시점에 한 번 발행</summary>
    public event Action<EnemyActor> Died;

    /// <summary>사망 또는 강제 취소로 Pool 반환이 필요할 때 한 번 발행</summary>
    public event Action<EnemyActor, EnemyReturnReason> ReturnRequested;

    private void Awake()
    {
        _health = GetComponent<Health>();
        _shield = GetComponent<Shield>();
        _knockbackReceiver = GetComponent<KnockbackReceiver>();
        _rigidbody2D = GetComponent<Rigidbody2D>();
        _stats = GetComponent<Stats>();
        _targetSensor = GetComponent<EnemyTargetSensor>();
        _decisionController = GetComponent<EnemyDecisionController>();
        _movementController = GetComponent<EnemyMovementController>();
        _attackController = GetComponent<EnemyAttackController>();
        _bossPhaseController = GetComponent<BossPhaseController>();
        _presentation = GetComponent<EnemyPresentation>();
        _hitFeedback = GetComponent<HitFeedback>();
        _gameplayColliders = GetComponentsInChildren<Collider2D>(true);

        CaptureLifecycleColliderStates();
    }

    // 저장된 Prefab에 RequireComponent가 소급되지 않는 경우를 초기화 실패로 변환
    private bool HasRequiredComponents()
    {
        if (_targetSensor == null)
        {
            Debug.LogError($"EnemyActor.Initialize: '{name}'에 EnemyTargetSensor가 없습니다.", this);
            return false;
        }

        if (_decisionController == null)
        {
            Debug.LogError($"EnemyActor.Initialize: '{name}'에 EnemyDecisionController가 없습니다.", this);
            return false;
        }

        if (_movementController == null)
        {
            Debug.LogError($"EnemyActor.Initialize: '{name}'에 EnemyMovementController가 없습니다.", this);
            return false;
        }

        if (_attackController == null)
        {
            Debug.LogError($"EnemyActor.Initialize: '{name}'에 EnemyAttackController가 없습니다.", this);
            return false;
        }

        if (_stats == null)
        {
            Debug.LogError($"EnemyActor.Initialize: '{name}'에 Stats가 없습니다.", this);
            return false;
        }

        return true;
    }

    private void OnDisable()
    {
        // 비활성화에서는 멱등 정리만 수행하고 반환 이벤트 재발행 방지
        PrepareForReturn();
    }

    private void Update()
    {
        if (LifecycleState == EnemyLifecycleState.Spawning)
        {
            if (_spawnCompletionSafeTimeout > 0f)
            {
                _spawnElapsed += Time.deltaTime;

                if (_spawnElapsed >= _spawnCompletionSafeTimeout)
                {
                    EnterActive();
                }
            }

            return;
        }

        if (LifecycleState == EnemyLifecycleState.Dying)
        {
            if (_deathCompletionSafeTimeout > 0f)
            {
                _deathElapsed += Time.deltaTime;

                if (_deathElapsed >= _deathCompletionSafeTimeout)
                {
                    RequestReturn(EnemyReturnReason.Killed);
                }
            }
        }
    }

    /// <summary>비활성 대여 직후 Archetype과 Spawn Context로 모든 런타임 구성 초기화</summary>
    public bool Initialize(EnemyArchetypeSO archetype, EnemySpawnContext spawnContext)
    {
        if (archetype == null)
        {
            Debug.LogError("EnemyActor.Initialize: archetype이 null입니다.", this);
            return false;
        }

        if (LifecycleState != EnemyLifecycleState.Inactive)
        {
            Debug.LogError($"EnemyActor.Initialize: '{name}'이 Inactive 상태가 아닌데 재초기화를 시도했습니다.", this);
            return false;
        }

        if (spawnContext.RuntimeConfig == null)
        {
            Debug.LogError($"EnemyActor.Initialize: '{name}'의 spawnContext.RuntimeConfig가 null입니다.", this);
            return false;
        }

        if (!HasRequiredComponents())
        {
            return false;
        }

        // 이전 대여의 구독·상태·물리 값 정리
        PrepareForReturn();

        // 이번 대여에 고정할 행동 설정과 외부 Context 보관
        Archetype = archetype;
        RuntimeConfig = spawnContext.RuntimeConfig;

        // 전투 수치는 Stats 초기화 결과만 사용해 Archetype과 이중 권위 방지
        _stats.Initialize();
        RuntimeStats stats = _stats.RuntimeStats;

        // Health를 Enemy 진영과 현재 Stats Snapshot으로 초기화
        _health.SetStartingHealth(Mathf.RoundToInt(stats.GetFinalValue(StatType.MaxHealth)));

        // Awake 이후 연결된 선택 컴포넌트 반영을 위해 대여 시점 재조회
        _shield = GetComponent<Shield>();
        _knockbackReceiver = GetComponent<KnockbackReceiver>();

        _shield?.SetStackCount(stats.ShieldStackCount);
        _knockbackReceiver?.SetResistance(stats.GetFinalValue(StatType.KnockbackResistance));
        _health.ConfigureInvulnerability(RuntimeConfig.IsHitInvulnerabilityEnabled, RuntimeConfig.HitInvulnerabilityDuration);
        _health.SetFaction(Faction.Enemy);

        // 선택적인 피격 연출은 존재할 때만 Health에 연결
        _hitFeedback?.Configure(_health, _spriteRenderer);

        // Sensor·Movement·Attack·Decision·Presentation 순서로 조립
        _targetSensor.Initialize(
            spawnContext.Target, RuntimeConfig.DetectionRange, RuntimeConfig.TrackingSightRange);
        _movementController.SetNavigation(spawnContext.Navigation);
        _attackController.Initialize(archetype, transform, Faction.Enemy, _health);

        if (_bossPhaseController != null && !_bossPhaseController.Initialize(archetype))
        {
            return false;
        }

        _attackController.PhaseChanged += HandleAttackPhaseChanged;
        _decisionController.StateChanged += HandleBehaviorStateChanged;

        if (_presentation != null)
        {
            _presentation.SpawnCompleted += HandleSpawnCompleted;
            _presentation.DeathCompleted += HandleDeathCompleted;
        }

        // 모든 구성 완료 후 Health 변경 구독
        _health.Changed += HandleHealthChanged;

        _hasConfirmedDeath = false;
        _spawnElapsed = 0f;
        _deathElapsed = 0f;

        // 게임플레이 진입 전 Spawning 상태 전환
        LifecycleState = EnemyLifecycleState.Spawning;

        // Spawn 연출 중 게임플레이 Collider 비활성화
        SetGameplayCollidersEnabled(false);

        _presentation?.OnLifecycleChanged(LifecycleState);

        // Spawn 완료 신호를 사용하지 않는 구성은 즉시 Active 전환
        if (!ShouldWaitForSpawnCompletion())
        {
            EnterActive();
        }

        return true;
    }

    // Presentation 설정에 따라 Spawn Animation Event 대기 여부 결정
    private bool ShouldWaitForSpawnCompletion()
    {
        return _presentation != null && _presentation.WaitForSpawnCompletion;
    }

    // Presentation 설정에 따라 Death Animation Event 대기 여부 결정
    private bool ShouldWaitForDeathCompletion()
    {
        return _presentation != null && _presentation.WaitForDeathCompletion;
    }

    // Spawn 완료 신호를 Spawning 상태에서 한 번만 수락
    private void HandleSpawnCompleted()
    {
        if (LifecycleState != EnemyLifecycleState.Spawning)
        {
            return;
        }

        EnterActive();
    }

    // Collider와 AI를 활성화하고 최종 MoveSpeed Snapshot 공급
    private void EnterActive()
    {
        LifecycleState = EnemyLifecycleState.Active;
        SetGameplayCollidersEnabled(true);

        float moveSpeed = _stats.RuntimeStats.GetFinalValue(StatType.MoveSpeed);
        // Active 진입 위치를 이번 대여의 Patrol 및 복귀 기준점으로 고정
        _decisionController.Activate(moveSpeed, RuntimeConfig, transform.position);

        _presentation?.OnLifecycleChanged(LifecycleState);
    }

    // 체력 감소를 표현에 전달하고 0 도달 시 사망 확정
    private void HandleHealthChanged(HealthChange change)
    {
        _presentation?.OnDamaged(change);

        if (LifecycleState != EnemyLifecycleState.Active || _hasConfirmedDeath || change.CurrentHealth > 0)
        {
            return;
        }

        ConfirmDeath();
    }

    // 공격·이동을 정지하고 Dying 전환 후 사망 이벤트 발행
    private void ConfirmDeath()
    {
        _hasConfirmedDeath = true;
        LifecycleState = EnemyLifecycleState.Dying;
        _deathElapsed = 0f;

        StopCombatAndMotion(EnemyAttackCancelReason.Death);

        _presentation?.OnLifecycleChanged(LifecycleState);

        Died?.Invoke(this);

        // 사망 연출 완료 신호를 사용하지 않는 구성은 즉시 반환 요청
        if (!ShouldWaitForDeathCompletion())
        {
            RequestReturn(EnemyReturnReason.Killed);
        }
    }

    private void HandleDeathCompleted()
    {
        if (LifecycleState != EnemyLifecycleState.Dying)
        {
            return;
        }

        RequestReturn(EnemyReturnReason.Killed);
    }

    /// <summary>Encounter 취소나 Scene 전환으로 처치 이벤트 없이 강제 반환 요청</summary>
    public void Cancel(EnemyReturnReason returnReason)
    {
        if (LifecycleState == EnemyLifecycleState.Inactive)
        {
            return;
        }

        StopCombatAndMotion(ToAttackCancelReason(returnReason));

        RequestReturn(returnReason);
    }

    // 사망과 강제 취소가 공유하는 전투·이동·Collider 종료 절차
    private void StopCombatAndMotion(EnemyAttackCancelReason attackCancelReason)
    {
        _decisionController.Deactivate();
        _attackController.Cancel(attackCancelReason);
        _movementController.Stop(Vector2.zero);
        SetGameplayCollidersEnabled(false);
        _rigidbody2D.linearVelocity = Vector2.zero;
        _rigidbody2D.angularVelocity = 0f;
    }

    // Actor 반환 이유를 Attack 취소 이유로 변환
    private static EnemyAttackCancelReason ToAttackCancelReason(EnemyReturnReason returnReason)
    {
        switch (returnReason)
        {
            case EnemyReturnReason.Killed:
                return EnemyAttackCancelReason.Death;
            case EnemyReturnReason.EncounterCancelled:
                return EnemyAttackCancelReason.EncounterCancelled;
            case EnemyReturnReason.SceneTransition:
                return EnemyAttackCancelReason.Deactivated;
            default:
                return EnemyAttackCancelReason.ConfigurationError;
        }
    }

    // Inactive 전환을 선행해 중복 ReturnRequested 발행 방지
    private void RequestReturn(EnemyReturnReason reason)
    {
        if (LifecycleState == EnemyLifecycleState.Inactive)
        {
            return;
        }

        PrepareForReturn();

        ReturnRequested?.Invoke(this, reason);
    }

    /// <summary>Pool 반환을 위해 모든 구독·Controller·물리·Collider 상태를 멱등 초기화</summary>
    public void PrepareForReturn()
    {
        if (_health != null)
        {
            _health.Changed -= HandleHealthChanged;
        }

        if (_attackController != null)
        {
            _attackController.PhaseChanged -= HandleAttackPhaseChanged;
        }

        if (_decisionController != null)
        {
            _decisionController.StateChanged -= HandleBehaviorStateChanged;
        }

        if (_presentation != null)
        {
            _presentation.SpawnCompleted -= HandleSpawnCompleted;
            _presentation.DeathCompleted -= HandleDeathCompleted;
        }

        // 부분 초기화 또는 필수 구성 누락 Actor의 OnDisable에서도 안전한 null 정리
        _targetSensor?.ClearTarget();
        _decisionController?.Deactivate();
        _bossPhaseController?.ResetRuntimeState();
        _attackController?.Cancel(EnemyAttackCancelReason.PoolReturned);
        _attackController?.ResetRuntimeState();
        _movementController?.ResetRuntimeState();
        _presentation?.ResetRuntimeState();

        SetGameplayCollidersEnabled(false);

        if (_rigidbody2D != null)
        {
            _rigidbody2D.linearVelocity = Vector2.zero;
            _rigidbody2D.angularVelocity = 0f;
        }

        if (_health != null)
        {
            _health.SetDamageAllowedPolicy(null);
            _health.ConfigureInvulnerability(false, 0f);
            _health.SetFaction(Faction.Neutral);
        }

        _shield?.ResetRuntimeState();

        Archetype = null;
        RuntimeConfig = null;
        _stats?.ResetRuntimeStats();
        _hasConfirmedDeath = false;
        _spawnElapsed = 0f;
        _deathElapsed = 0f;

        LifecycleState = EnemyLifecycleState.Inactive;
    }

    private void HandleBehaviorStateChanged(EnemyBehaviorState state)
    {
        _presentation?.OnBehaviorChanged(state);
    }

    private void HandleAttackPhaseChanged(EnemyAttackPhase phase)
    {
        _presentation?.OnAttackPhaseChanged(phase);
    }

    /// <summary>Prefab 저작 상태를 Active 복귀용 Collider 기준값으로 한 번 포착</summary>
    private void CaptureLifecycleColliderStates()
    {
        if (_lifecycleColliderStates != null)
        {
            return;
        }

        _lifecycleColliderStates = new bool[_gameplayColliders.Length];

        for (int i = 0; i < _gameplayColliders.Length; i++)
        {
            _lifecycleColliderStates[i] = _gameplayColliders[i] != null && _gameplayColliders[i].enabled;
        }
    }

    // 비활성화는 모든 Collider를 끄고 활성화는 포착한 기준 상태만 복원해 Action 전용 Collider 보호
    private void SetGameplayCollidersEnabled(bool isEnabled)
    {
        for (int i = 0; i < _gameplayColliders.Length; i++)
        {
            if (_gameplayColliders[i] == null)
            {
                continue;
            }

            _gameplayColliders[i].enabled =
                isEnabled && _lifecycleColliderStates != null && _lifecycleColliderStates[i];
        }
    }
}
