using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

/// <summary>
/// 이번 Build에서 생성한 RoomRuntime과 GameObject의 정리 권한을 소유한다.
/// </summary>
public sealed class DungeonAssembly : IDisposable
{
    private readonly ReadOnlyCollection<RoomRuntime> _rooms;
    private readonly GameObject _generatedRoot;
    private DungeonEnemyDeployment _enemyDeployment;
    private bool _disposed;

    public IReadOnlyList<RoomRuntime> Rooms => _rooms;
    public DungeonEnemyDeployment EnemyDeployment => _enemyDeployment;

    // Edgar 전용 생성 Root 단위로 정리 권한을 인수한다. 생성 Instance는 모두 Root의 자식이므로 Root
    // 파괴만으로 함께 정리된다.
    internal DungeonAssembly(IList<RoomRuntime> rooms, GameObject generatedRoot)
    {
        _rooms = new ReadOnlyCollection<RoomRuntime>(new List<RoomRuntime>(rooms));
        _generatedRoot = generatedRoot;
    }

    // 원자적으로 Spawn된 Dungeon 전체 Enemy Deployment의 정리 권한을 이어받는다. 호출은 Build마다 한 번만 한다.
    internal void AttachEnemyDeployment(DungeonEnemyDeployment deployment)
    {
        _enemyDeployment = deployment;
    }

    // Boss Portal 전환에서 물리 Stage의 Enemy만 먼저 Pool에 반환한다. Rooms·생성 Root는 그대로 둬 이후
    // Root 비활성화·Dispose가 정리하게 한다(dungeon_system-design.md 4.2a 단방향 전환 순서).
    internal void ReleasePhysicalEnemies()
    {
        _enemyDeployment?.Dispose();
        _enemyDeployment = null;
    }

    // Boss Portal 전환 성공 뒤 물리 생성 Root 전체를 비활성화한다. 전환은 단방향이므로 다시 활성화하지 않는다.
    internal void SetRootActive(bool isActive)
    {
        _generatedRoot?.SetActive(isActive);
    }

    // Enemy Deployment를 먼저 해제한 뒤 생성한 GameObject를 제거한다. RoomRuntime은 계획·View 참조만
    // 들고 있어 따로 정리할 상태가 없다.
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _enemyDeployment?.Dispose();
        _enemyDeployment = null;

        DestroyInstance(_generatedRoot);
    }

    // 실행 환경에 맞는 Unity 파괴 방식으로 조립 인스턴스를 제거한다.
    internal static void DestroyInstance(GameObject instance)
    {
        if (instance == null)
        {
            return;
        }

        // Destroy는 실제 파괴를 프레임 끝까지 미루므로, Collider·Trigger를 같은 프레임의
        // 물리 질의에서 즉시 제외하기 위해 비활성화를 먼저 적용한다.
        instance.SetActive(false);

        if (Application.isPlaying)
        {
            UnityEngine.Object.Destroy(instance);
        }
        else
        {
            UnityEngine.Object.DestroyImmediate(instance);
        }
    }
}
