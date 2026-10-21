using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 게시판 슬롯에서 Dungeon 정의와 난이도를 선택하는 정적 진입 데이터.
/// 조건부 규칙과 보상 데이터의 최종 표현은 별도 계약 확정 전까지 소유하지 않는다.
/// Difficulty Modifier 참조는 적용 소유 시스템이 확정되지 않아 이번 단계에서 추가하지 않는다.
/// </summary>
[CreateAssetMenu(fileName = "WantedPoster_", menuName = "Scriptable Objects/Dungeon/Wanted Poster")]
public sealed class WantedPosterSO : ScriptableObject
{
    [Space(5)]
    [Header("콘텐츠 식별")]
    [Tooltip("세이브·참조에 사용할 안정적인 콘텐츠 ID.")]
    [SerializeField] private string _contentId;

    [Space(10)]
    [Header("던전 진입")]
    [SerializeField] private WantedPosterType _posterType;
    [SerializeField] private WantedPosterSlotType _slotType;
    [SerializeField] private DungeonDifficulty _difficulty;
    [SerializeField] private DungeonDefinitionSO _dungeonDefinition;

    [Space(10)]
    [Header("Enemy 구성")]
    [Tooltip("일반 배치 Enemy 후보와 가중치 목록.")]
    [SerializeField] private WantedPosterEnemyCandidate[] _enemyCandidates;
    [Tooltip("일반 배치 중 Elite 비율(0~1).")]
    [Range(0f, 1f)]
    [SerializeField] private float _eliteRatio;

    [Space(10)]
    [Header("목표")]
    [SerializeField] private DungeonObjectiveType _objectiveType;
    [Tooltip("ObjectiveType이 Target일 때만 사용하는 전용 Enemy Archetype.")]
    [SerializeField] private EnemyArchetypeSO _targetEnemyArchetype;
    [Tooltip("ObjectiveType이 Boss일 때만 사용하는 전용 Enemy Archetype.")]
    [SerializeField] private EnemyArchetypeSO _bossEnemyArchetype;

    public string ContentId => _contentId;
    public WantedPosterType PosterType => _posterType;
    public WantedPosterSlotType SlotType => _slotType;
    public DungeonDifficulty Difficulty => _difficulty;
    public DungeonDefinitionSO DungeonDefinition => _dungeonDefinition;
    public IReadOnlyList<WantedPosterEnemyCandidate> EnemyCandidates => _enemyCandidates;
    public float EliteRatio => _eliteRatio;
    public DungeonObjectiveType ObjectiveType => _objectiveType;
    public EnemyArchetypeSO TargetEnemyArchetype => _targetEnemyArchetype;
    public EnemyArchetypeSO BossEnemyArchetype => _bossEnemyArchetype;
}
