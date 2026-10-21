using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 챕터별 게시판 후보와 보스 해금 조건의 정적 정의.
/// </summary>
[CreateAssetMenu(fileName = "ChapterDefinition_", menuName = "Scriptable Objects/Dungeon/Chapter Definition")]
public sealed class ChapterDefinitionSO : ScriptableObject
{
    [Space(5)]
    [Header("콘텐츠 식별")]
    [Tooltip("세이브·참조에 사용할 안정적인 콘텐츠 ID.")]
    [SerializeField] private string _contentId;

    [Space(10)]
    [Header("수배서 Pool")]
    [Tooltip("일반 슬롯 두 개에 배치할 수배서 후보.")]
    [SerializeField] private WantedPosterSO[] _normalWantedPosters;
    [Tooltip("보스 해금 전 특수 슬롯에 배치할 조건부 수배서 후보.")]
    [SerializeField] private WantedPosterSO[] _conditionalWantedPosters;
    [Tooltip("해금 후 특수 슬롯에 유지할 보스 수배서.")]
    [SerializeField] private WantedPosterSO _bossWantedPoster;
    [Min(1)]
    [SerializeField] private int _bossUnlockRequiredClearCount = 1;

    public string ContentId => _contentId;
    public IReadOnlyList<WantedPosterSO> NormalWantedPosters => _normalWantedPosters;
    public IReadOnlyList<WantedPosterSO> ConditionalWantedPosters => _conditionalWantedPosters;
    public WantedPosterSO BossWantedPoster => _bossWantedPoster;
    public int BossUnlockRequiredClearCount => _bossUnlockRequiredClearCount;
}
