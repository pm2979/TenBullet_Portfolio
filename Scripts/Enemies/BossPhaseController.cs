using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Boss 체력 감소에 따라 Phase 공격을 누적 해금하고 최종 Entry 패턴을 예약</summary>
// 공격 선택·실행·취소는 EnemyAttackController에 유지하고 Phase 진행 상태만 소유
[RequireComponent(typeof(Health))]
[RequireComponent(typeof(EnemyAttackController))]
[DisallowMultipleComponent]
public sealed class BossPhaseController : MonoBehaviour
{
    private Health _health;
    private EnemyAttackController _attackController;
    private IReadOnlyList<BossPhaseDefinition> _phases;

    public event Action<int> PhaseChanged;

    public int CurrentPhaseIndex { get; private set; } = -1;
    public int CurrentPhaseNumber => CurrentPhaseIndex + 1;
    public bool IsInitialized { get; private set; }

    private void Awake()
    {
        _health = GetComponent<Health>();
        _attackController = GetComponent<EnemyAttackController>();
    }

    /// <summary>Archetype의 Boss Phase를 초기화하고 체력 변경 관찰 시작</summary>
    public bool Initialize(EnemyArchetypeSO archetype)
    {
        ResetRuntimeState();

        if (archetype == null || archetype.MonsterGrade != MonsterGrade.Boss ||
            archetype.BossPhases == null || archetype.BossPhases.Count == 0 ||
            _health == null || _attackController == null)
        {
            Debug.LogError("BossPhaseController.Initialize: 유효한 Boss Archetype과 Phase 구성이 필요합니다.", this);
            return false;
        }

        _phases = archetype.BossPhases;
        IsInitialized = true;
        _health.Changed += HandleHealthChanged;
        ApplyReachedPhases(_health.GetCurrentHealth(), _health.GetMaxHealth());
        return true;
    }

    /// <summary>체력 구독과 현재 Phase 진행 상태를 Pool 재사용 전 상태로 초기화</summary>
    public void ResetRuntimeState()
    {
        if (_health != null)
        {
            _health.Changed -= HandleHealthChanged;
        }

        _phases = null;
        CurrentPhaseIndex = -1;
        IsInitialized = false;
    }

    private void OnDisable()
    {
        ResetRuntimeState();
    }

    private void HandleHealthChanged(HealthChange change)
    {
        if (!IsInitialized || change.CurrentHealth <= 0 || change.MaxHealth <= 0)
        {
            return;
        }

        ApplyReachedPhases(change.CurrentHealth, change.MaxHealth);
    }

    // 한 번의 피해로 통과한 모든 Phase를 순서대로 적용하고 마지막 Entry만 예약
    private void ApplyReachedPhases(int currentHealth, int maximumHealth)
    {
        if (!IsInitialized || maximumHealth <= 0)
        {
            return;
        }

        float healthRatio = Mathf.Clamp01((float)currentHealth / maximumHealth);
        int finalReachedIndex = CurrentPhaseIndex;
        EnemyAttackDefinitionSO finalEntryPattern = null;

        while (finalReachedIndex + 1 < _phases.Count)
        {
            BossPhaseDefinition nextPhase = _phases[finalReachedIndex + 1];

            if (nextPhase == null || healthRatio > nextPhase.EnterAtOrBelowHealthRatio)
            {
                break;
            }

            finalReachedIndex++;
            UnlockPatterns(nextPhase.UnlockPatterns);
            finalEntryPattern = nextPhase.EntryPattern;
        }

        if (finalReachedIndex == CurrentPhaseIndex)
        {
            return;
        }

        CurrentPhaseIndex = finalReachedIndex;

        if (finalEntryPattern != null)
        {
            _attackController.ReserveEntryPattern(finalEntryPattern);
        }

        PhaseChanged?.Invoke(CurrentPhaseNumber);
    }

    // Phase에 포함된 유효 공격을 기존 해금 상태에 누적
    private void UnlockPatterns(IReadOnlyList<EnemyAttackDefinitionSO> patterns)
    {
        if (patterns == null)
        {
            return;
        }

        for (int i = 0; i < patterns.Count; i++)
        {
            _attackController.Unlock(patterns[i]);
        }
    }
}
