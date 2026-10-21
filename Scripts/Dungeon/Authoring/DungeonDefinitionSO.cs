using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Seed 기반 Dungeon 계획 생성에 필요한 범위와 Room 후보의 정적 정의.
/// </summary>
[CreateAssetMenu(fileName = "DungeonDefinition_", menuName = "Scriptable Objects/Dungeon/Dungeon Definition")]
public sealed class DungeonDefinitionSO : ScriptableObject
{
    [Space(5)]
    [Header("콘텐츠 식별")]
    [Tooltip("세이브·참조에 사용할 안정적인 콘텐츠 ID.")]
    [SerializeField] private string _contentId;
    [SerializeField] private DungeonType _dungeonType;
    [Min(1)]
    [SerializeField] private int _generationVersion = 1;

    [Space(10)]
    [Header("생성 범위")]
    [Min(1)]
    [SerializeField] private int _minimumContentRoomCount = 1;
    [Min(1)]
    [SerializeField] private int _maximumContentRoomCount = 1;

    [Space(10)]
    [Header("Room Pool")]
    [SerializeField] private RoomArchetypeSO[] _roomPool;

    [Space(10)]
    [Header("보스 던전")]
    [Tooltip("Boss Dungeon의 마지막에 절차적 변형 없이 배치할 고정 Arena Room.")]
    [SerializeField] private RoomArchetypeSO _bossArenaRoom;

    public string ContentId => _contentId;
    public DungeonType DungeonType => _dungeonType;
    public int GenerationVersion => _generationVersion;
    public int MinimumContentRoomCount => _minimumContentRoomCount;
    public int MaximumContentRoomCount => _maximumContentRoomCount;
    public IReadOnlyList<RoomArchetypeSO> RoomPool => _roomPool;
    public RoomArchetypeSO BossArenaRoom => _bossArenaRoom;
}
