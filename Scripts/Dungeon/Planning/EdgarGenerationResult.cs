using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Edgar 공간 생성·변환의 성공 여부와 결과 <see cref="DungeonBuildPlan"/> 또는 구조화된 실패를 전달한다.
/// Edgar 타입을 참조하지 않는다.
/// </summary>
/// <remarks>
/// 성공 시 Edgar가 실제로 Instantiate한 Room GameObject(<see cref="RoomInstancesByRoomId"/>)와 그 전용
/// 생성 Root(<see cref="GeneratedRoot"/>)를 파괴하지 않고 그대로 넘긴다. 4단계부터 Runtime이 이 Instance를
/// 재사용해 Binding하므로, 호출자가 성공 결과를 더 이상 쓰지 않게 되면 <see cref="GeneratedRoot"/>의
/// 정리 책임을 진다(바인딩 성공 시 <c>DungeonAssembly</c>, 바인딩 전 중단 시 호출자 자신).
/// </remarks>
public sealed class EdgarGenerationResult
{
    public bool Succeeded { get; }
    public DungeonBuildPlan Plan { get; }
    public IReadOnlyDictionary<string, GameObject> RoomInstancesByRoomId { get; }
    public GameObject GeneratedRoot { get; }
    public EdgarGenerationFailure Failure { get; }

    // Edgar 결과를 프로젝트 DTO로 변환까지 마친 완전한 계획 결과를 만든다. 생성 Instance와 Root는
    // 파괴되지 않은 상태로 호출자에게 소유권이 넘어간다.
    internal static EdgarGenerationResult Success(
        DungeonBuildPlan plan,
        IReadOnlyDictionary<string, GameObject> roomInstancesByRoomId,
        GameObject generatedRoot)
    {
        return new EdgarGenerationResult(
            true, plan, roomInstancesByRoomId, generatedRoot, EdgarGenerationFailure.None);
    }

    // 계획과 생성 Instance를 노출하지 않고 식별 가능한 실패 결과를 만든다.
    internal static EdgarGenerationResult Failed(EdgarGenerationFailure failure)
    {
        return new EdgarGenerationResult(false, null, null, null, failure);
    }

    private EdgarGenerationResult(
        bool succeeded,
        DungeonBuildPlan plan,
        IReadOnlyDictionary<string, GameObject> roomInstancesByRoomId,
        GameObject generatedRoot,
        EdgarGenerationFailure failure)
    {
        Succeeded = succeeded;
        Plan = plan;
        RoomInstancesByRoomId = roomInstancesByRoomId;
        GeneratedRoot = generatedRoot;
        Failure = failure;
    }
}
