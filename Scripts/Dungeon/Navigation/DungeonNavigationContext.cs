/// <summary>
/// 한 Dungeon의 Navigation 준비 상태와 생성 식별 정보를 제공한다.
/// </summary>
public sealed class DungeonNavigationContext
{
    public string DungeonId { get; }
    public int Seed { get; }
    public int GenerationVersion { get; }
    public bool IsReady { get; private set; }
    public bool IsDisposed { get; private set; }

    // BuildPlan에서 외부 A* 타입을 노출하지 않는 Dungeon 식별 정보를 복사한다.
    internal DungeonNavigationContext(DungeonBuildPlan plan)
    {
        DungeonId = plan.Definition.ContentId;
        Seed = plan.Seed;
        GenerationVersion = plan.GenerationVersion;
    }

    // Graph Scan과 Room 연결이 끝난 Context를 요청 가능 상태로 전환한다.
    internal void MarkReady()
    {
        if (!IsDisposed)
        {
            IsReady = true;
        }
    }

    // 새 요청을 차단하고 Dungeon 수명이 끝났음을 기록한다.
    internal void MarkDisposed()
    {
        IsReady = false;
        IsDisposed = true;
    }
}
