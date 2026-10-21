using System.Collections.Generic;
using System.Collections.ObjectModel;

/// <summary>
/// Room·좌표·연결·Encounter 입력을 조립 단계에 전달하는 불변 Dungeon 계획.
/// </summary>
public sealed class DungeonBuildPlan
{
    private readonly ReadOnlyCollection<DungeonRoomPlan> _rooms;
    private readonly ReadOnlyCollection<DungeonConnectionPlan> _connections;

    public DungeonDefinitionSO Definition { get; }
    public int Seed { get; }
    public int GenerationVersion { get; }
    public int ContentRoomCount { get; }
    public int RequiredCombatRoomCount { get; }
    public int OptionalBranchCount { get; }
    public IReadOnlyList<DungeonRoomPlan> Rooms => _rooms;
    public IReadOnlyList<DungeonConnectionPlan> Connections => _connections;

    /// <summary>Boss Dungeon에서 Boss Portal이 활성화되는 마지막 Combat Room ID. Normal Dungeon은 null이다.</summary>
    public string PortalTargetRoomId { get; }

    // 완성된 계획 값을 복사해 조립 전 외부 변경을 차단한다.
    internal DungeonBuildPlan(
        DungeonDefinitionSO definition,
        int seed,
        int generationVersion,
        DungeonTopologyPlan layout,
        IList<DungeonRoomPlan> rooms,
        IList<DungeonConnectionPlan> connections)
    {
        Definition = definition;
        Seed = seed;
        GenerationVersion = generationVersion;
        ContentRoomCount = layout.ContentRoomCount;
        RequiredCombatRoomCount = layout.RequiredCombatRoomCount;
        OptionalBranchCount = layout.OptionalBranchCount;
        PortalTargetRoomId = layout.PortalTargetRoomId;
        _rooms = new ReadOnlyCollection<DungeonRoomPlan>(new List<DungeonRoomPlan>(rooms));
        _connections = new ReadOnlyCollection<DungeonConnectionPlan>(
            new List<DungeonConnectionPlan>(connections));
    }
}
