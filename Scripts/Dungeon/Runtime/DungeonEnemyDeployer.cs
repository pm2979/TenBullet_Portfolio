using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// DungeonEnemyPlan 전체를 한 번의 원자적 Spawn 작업으로 실행한다. 하나라도 실패하면 이번 호출에서
/// 대여한 Enemy를 모두 되돌리고 부분 Deployment를 노출하지 않는다.
/// </summary>
public sealed class DungeonEnemyDeployer
{
    public DungeonEnemyDeploymentResult Deploy(
        DungeonEnemyPlan plan,
        IReadOnlyList<RoomRuntime> rooms,
        EnemyInstanceProvider instanceProvider,
        Transform target,
        Func<EnemyActor, IEnemyNavigation> navigationFactory)
    {
        if (plan == null || plan.Entries == null || rooms == null || rooms.Count == 0 ||
            instanceProvider == null || target == null || navigationFactory == null)
        {
            return DungeonEnemyDeploymentResult.Failed(DungeonEnemyDeploymentFailure.InvalidConfiguration);
        }

        Dictionary<string, RoomView> roomViewsByRoomId = BuildRoomViewLookup(rooms);
        List<DungeonEnemyDeploymentEntry> deploymentEntries =
            new List<DungeonEnemyDeploymentEntry>(plan.Entries.Count);

        for (int i = 0; i < plan.Entries.Count; i++)
        {
            DungeonEnemySpawnEntry entry = plan.Entries[i];

            if (!TryResolveSpawnPoint(
                    roomViewsByRoomId, entry.RoomId, entry.SpawnPointId, out Transform spawnPoint))
            {
                ReturnAll(deploymentEntries, instanceProvider);
                return DungeonEnemyDeploymentResult.Failed(DungeonEnemyDeploymentFailure.SpawnPointMissing);
            }

            EnemyRentResult rentResult = instanceProvider.RentWithContextFactory(
                entry.Archetype,
                actor => CreateSpawnContext(actor, entry.Archetype, target, navigationFactory),
                spawnPoint.position,
                spawnPoint.rotation);

            if (!rentResult.IsSuccess)
            {
                ReturnAll(deploymentEntries, instanceProvider);
                return DungeonEnemyDeploymentResult.Failed(DungeonEnemyDeploymentFailure.EnemyRentFailed);
            }

            deploymentEntries.Add(new DungeonEnemyDeploymentEntry(entry, rentResult.Actor));
        }

        DungeonEnemyDeployment deployment = new DungeonEnemyDeployment(instanceProvider);
        deployment.Track(deploymentEntries);
        return DungeonEnemyDeploymentResult.Success(deployment);
    }

    // RoomId로 Spawn Point 해석에 필요한 RoomView만 모은다.
    private static Dictionary<string, RoomView> BuildRoomViewLookup(IReadOnlyList<RoomRuntime> rooms)
    {
        Dictionary<string, RoomView> lookup = new Dictionary<string, RoomView>(StringComparer.Ordinal);

        for (int i = 0; i < rooms.Count; i++)
        {
            RoomRuntime room = rooms[i];

            if (room?.View != null)
            {
                lookup[room.RoomId] = room.View;
            }
        }

        return lookup;
    }

    private static EnemySpawnContext CreateSpawnContext(
        EnemyActor actor,
        EnemyArchetypeSO archetype,
        Transform target,
        Func<EnemyActor, IEnemyNavigation> navigationFactory)
    {
        IEnemyNavigation navigation = navigationFactory(actor);

        if (navigation == null)
        {
            throw new InvalidOperationException("Enemy Navigation을 만들 수 없습니다.");
        }

        return new EnemySpawnContext(target, new EnemyRuntimeConfig(archetype), navigation);
    }

    // RoomId로 해당 Room View를 찾아 SpawnPointId의 Transform을 해석한다.
    private static bool TryResolveSpawnPoint(
        IReadOnlyDictionary<string, RoomView> roomViewsByRoomId,
        string roomId,
        string spawnPointId,
        out Transform spawnTransform)
    {
        spawnTransform = null;

        if (roomId == null ||
            !roomViewsByRoomId.TryGetValue(roomId, out RoomView roomView) || roomView == null)
        {
            return false;
        }

        return roomView.TryGetSpawnPoint(spawnPointId, out spawnTransform) && spawnTransform != null;
    }

    // 중간 실패 시 이미 대여한 Actor를 역순으로 반환한다. Actor 반환 과정에서 Movement가 보유한
    // Navigation Runtime 상태도 함께 정리된다.
    private static void ReturnAll(
        IReadOnlyList<DungeonEnemyDeploymentEntry> entries,
        EnemyInstanceProvider instanceProvider)
    {
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            instanceProvider.Return(entries[i].Actor, EnemyReturnReason.EncounterCancelled);
        }
    }
}
