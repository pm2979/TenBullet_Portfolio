using System;
using System.Collections.Generic;

/// <summary>
/// 원자적으로 Spawn된 Dungeon 전체 Enemy Actor의 정리 권한을 소유한다
/// </summary>
public sealed class DungeonEnemyDeployment : IDisposable
{
    private readonly EnemyInstanceProvider _instanceProvider;
    private readonly List<DungeonEnemyDeploymentEntry> _entries = new List<DungeonEnemyDeploymentEntry>();
    private readonly List<EnemyActor> _activeActors = new List<EnemyActor>();
    private bool _disposed;

    internal DungeonEnemyDeployment(EnemyInstanceProvider instanceProvider)
    {
        _instanceProvider = instanceProvider ?? throw new ArgumentNullException(nameof(instanceProvider));
    }

    public IReadOnlyList<DungeonEnemyDeploymentEntry> Entries => _entries;

    // 전체 원자적 Spawn 성공 직후 호출해 배정 결과 전체를 추적 대상으로 등록한다.
    internal void Track(IReadOnlyList<DungeonEnemyDeploymentEntry> entries)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            DungeonEnemyDeploymentEntry entry = entries[i];
            _entries.Add(entry);
            _activeActors.Add(entry.Actor);
            entry.Actor.ReturnRequested += HandleActorReturnRequested;
        }
    }

    // 사망·강제 반환 어느 경로로도 Pool에 반환된 Actor를 즉시 활성 집합에서 제거해, Dispose 시점에
    // 이미 다른 용도로 재대여됐을 수 있는 Actor를 실수로 다시 반환하지 않게 한다.
    private void HandleActorReturnRequested(EnemyActor actor, EnemyReturnReason reason)
    {
        UntrackActor(actor);
    }

    private void UntrackActor(EnemyActor actor)
    {
        actor.ReturnRequested -= HandleActorReturnRequested;
        _activeActors.Remove(actor);
    }

    // 이 시점까지 살아남은 Actor만 역순으로 강제 반환한다. 이미 반환된 Actor는 대상에서 제외돼 있다.
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        EnemyActor[] actors = _activeActors.ToArray();

        for (int i = actors.Length - 1; i >= 0; i--)
        {
            UntrackActor(actors[i]);
            _instanceProvider.Return(actors[i], EnemyReturnReason.EncounterCancelled);
        }

        _activeActors.Clear();
    }
}
