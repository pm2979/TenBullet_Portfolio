using System.Collections.Generic;
using UnityEngine;

/// <summary>Boss HP 임계값별 공격 해금 목록과 일회성 Entry 패턴을 정의</summary>
// Archetype이 소유하는 정적 직렬화 데이터이며 Phase 런타임 상태는 Controller가 관리
[System.Serializable]
public sealed class BossPhaseDefinition
{
    [Tooltip("이 Phase에 진입하는 체력 비율 조건. currentHealth/maxHealth가 이 값 이하가 되면 진입한다(Phase 1은 1.0)")]
    [Range(0f, 1f)]
    [SerializeField] private float _enterAtOrBelowHealthRatio;

    [Tooltip("이 Phase 진입 시 한 번 예약할 Entry 공격. 없으면 일반 후보 선택으로만 진행한다")]
    [SerializeField] private EnemyAttackDefinitionSO _entryPattern;

    [Tooltip("이 Phase 진입 시 추가로 해금할 공격 목록. 같은 Archetype의 attackCatalog에 포함되어 있어야 한다")]
    [SerializeField] private EnemyAttackDefinitionSO[] _unlockPatterns;

    public float EnterAtOrBelowHealthRatio => _enterAtOrBelowHealthRatio;
    public EnemyAttackDefinitionSO EntryPattern => _entryPattern;
    public IReadOnlyList<EnemyAttackDefinitionSO> UnlockPatterns => _unlockPatterns;
}
