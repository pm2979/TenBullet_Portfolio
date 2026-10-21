using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Tilemaps;

/// <summary>
/// 게임 전역 정적 데이터의 루트가 되는 단일 Addressable 데이터베이스
/// </summary>
[CreateAssetMenu(fileName = "GameDatabase", menuName = "Top Down Combat/Game Database")]
public sealed class GameDatabaseSO : ScriptableObject
{

    [Space(5)]
    [Header("UI")]
    [Tooltip("플레이어 체력 UI 프리팹")]
    public GameObject playerHealthIconPrefab;

    [Space(5)]
    [Header("Stats")]
    [Tooltip("게임에 등록된 전체 Stats 기본값 정의 목록")]
    [SerializeField] private StatsSO[] _statsDefinitions;

    [Space(5)]
    [Header("Enemy")]
    [Tooltip("게임에 등록된 전체 Enemy 정적 데이터 목록. Enemy 콘텐츠 그래프의 단일 진입점이다")]
    [SerializeField] private EnemyArchetypeSO[] _enemyArchetypes;

    [Space(5)]
    [Header("Dungeon")]
    [Tooltip("게임에 등록된 전체 챕터 정의 목록.")]
    [SerializeField] private ChapterDefinitionSO[] _chapterDefinitions;
    [Tooltip("게임에 등록된 전체 수배서 정의 목록.")]
    [SerializeField] private WantedPosterSO[] _wantedPosters;
    [Tooltip("게임에 등록된 전체 던전 정의 목록.")]
    [SerializeField] private DungeonDefinitionSO[] _dungeonDefinitions;
    [Tooltip("게임에 등록된 전체 Room 정의 목록.")]
    [SerializeField] private RoomArchetypeSO[] _roomArchetypes;

    public IReadOnlyList<StatsSO> StatsDefinitions => _statsDefinitions;
    public IReadOnlyList<EnemyArchetypeSO> EnemyArchetypes => _enemyArchetypes;
    public IReadOnlyList<ChapterDefinitionSO> ChapterDefinitions => _chapterDefinitions;
    public IReadOnlyList<WantedPosterSO> WantedPosters => _wantedPosters;
    public IReadOnlyList<DungeonDefinitionSO> DungeonDefinitions => _dungeonDefinitions;
    public IReadOnlyList<RoomArchetypeSO> RoomArchetypes => _roomArchetypes;
}
