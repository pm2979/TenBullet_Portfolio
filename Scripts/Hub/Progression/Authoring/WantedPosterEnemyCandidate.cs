using System;
using UnityEngine;

/// <summary>
/// WantedPoster가 보유하는 일반 Enemy 배치 후보 하나. 등급은 Archetype의 MonsterGrade를 그대로 따른다.
/// </summary>
[Serializable]
public sealed class WantedPosterEnemyCandidate
{
    [Tooltip("배치 후보 Enemy Archetype.")]
    [SerializeField] private EnemyArchetypeSO _archetype;
    [Tooltip("가중치 선택에 사용할 값. 0보다 커야 합니다.")]
    [SerializeField] private float _weight;

    public EnemyArchetypeSO Archetype => _archetype;
    public float Weight => _weight;
}
