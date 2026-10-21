using System;
using System.Collections.Generic;

/// <summary>
/// 원자적으로 Spawn된 Enemy 배정 결과에서 ObjectiveType별 완료 조건만 관찰하는 구체 객체.
/// Room Role, Room Clear 수, Exit Room을 사용하지 않는다.
/// </summary>
public sealed class DungeonObjectiveTracker : IDisposable
{
    private readonly DungeonObjectiveType _objectiveType;
    private readonly HashSet<EnemyActor> _regularEnemies = new HashSet<EnemyActor>();
    private readonly EnemyActor _targetActor;
    private EnemyActor _bossActor;
    private bool _disposed;

    public event Action Completed;

    public bool IsCompleted { get; private set; }

    // ObjectiveType과 성공한 Spawn 결과로 Kind별 감시 대상을 구분해 등록하고 가장 좁은 신호(Died)만 구독한다.
    // Died는 확정된 처치에서만 발행되므로 강제 Pool 반환·취소와 섞이지 않는다.
    public DungeonObjectiveTracker(DungeonObjectiveType objectiveType, IReadOnlyList<DungeonEnemyDeploymentEntry> entries)
    {
        _objectiveType = objectiveType;

        if (entries == null)
        {
            return;
        }

        for (int i = 0; i < entries.Count; i++)
        {
            DungeonEnemyDeploymentEntry entry = entries[i];

            if (entry?.Actor == null || entry.PlanEntry == null)
            {
                continue;
            }

            switch (entry.PlanEntry.Kind)
            {
                case DungeonSpawnPointKind.RegularEnemy:
                    if (_regularEnemies.Add(entry.Actor))
                    {
                        entry.Actor.Died += HandleRegularEnemyDied;
                    }

                    break;
                case DungeonSpawnPointKind.Target:
                    _targetActor = entry.Actor;
                    _targetActor.Died += HandleTargetDied;
                    break;
                case DungeonSpawnPointKind.Boss:
                    _bossActor = entry.Actor;
                    _bossActor.Died += HandleBossDied;
                    break;
            }
        }
    }

    // Boss는 Arena Navigation 준비 성공 후 한 번만 Spawn되므로 생성 시점에는 Entry가 없다(STEP-03). Portal
    // 전환이 실제 Boss Actor를 만든 뒤에만 이 메서드로 감시 대상에 등록한다 - 등록 전에는 Boss 목표가 완료될
    // 수 없다(FR-11). Boss 목표가 아니거나 이미 등록된 경우, Dispose된 경우는 무시한다.
    internal void RegisterBossActor(EnemyActor bossActor)
    {
        if (_disposed || bossActor == null || _objectiveType != DungeonObjectiveType.Boss || _bossActor != null)
        {
            return;
        }

        _bossActor = bossActor;
        _bossActor.Died += HandleBossDied;
    }

    // 모든 Actor의 Died 구독을 해제한다. Pool 반환 자체는 DungeonEnemyDeployment이 소유하므로 여기서 하지 않는다.
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        foreach (EnemyActor actor in _regularEnemies)
        {
            actor.Died -= HandleRegularEnemyDied;
        }

        _regularEnemies.Clear();

        if (_targetActor != null)
        {
            _targetActor.Died -= HandleTargetDied;
        }

        if (_bossActor != null)
        {
            _bossActor.Died -= HandleBossDied;
        }

        Completed = null;
    }

    // AllEnemies 목표에서만 일반 배치 Enemy가 모두 사망했을 때 완료한다.
    private void HandleRegularEnemyDied(EnemyActor actor)
    {
        actor.Died -= HandleRegularEnemyDied;
        _regularEnemies.Remove(actor);

        if (_objectiveType == DungeonObjectiveType.AllEnemies && _regularEnemies.Count == 0)
        {
            Complete();
        }
    }

    // Target 목표에서만 Target 사망으로 완료한다. 남은 일반 Enemy는 Deployment가 계속 추적·정리한다.
    private void HandleTargetDied(EnemyActor actor)
    {
        if (_objectiveType == DungeonObjectiveType.Target)
        {
            Complete();
        }
    }

    // Boss 목표에서만 Boss 사망으로 완료한다. 남은 일반 Enemy는 Deployment가 계속 추적·정리한다.
    private void HandleBossDied(EnemyActor actor)
    {
        if (_objectiveType == DungeonObjectiveType.Boss)
        {
            Complete();
        }
    }

    // 상태가 바뀌는 최초 한 번만 완료 이벤트를 발행한다.
    private void Complete()
    {
        if (IsCompleted)
        {
            return;
        }

        IsCompleted = true;
        Completed?.Invoke();
    }
}
