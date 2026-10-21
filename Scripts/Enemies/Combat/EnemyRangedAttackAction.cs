using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>고정된 AimSnapshot으로 단발·연사·분산·원형 Projectile 출력을 실행</summary>
// 기존 Projectile 생성 경계를 재사용하고 마지막 발사 결과만 Attack Controller에 통지
[RequireComponent(typeof(EnemyAttackController))]
[DisallowMultipleComponent]
public sealed class EnemyRangedAttackAction : MonoBehaviour, IEnemyAttackAction
{
    [Tooltip("투사체가 생성되는 발사 원점")]
    [SerializeField] private Transform _muzzle;

    private readonly ProjectileEmissionPlanner _emissionPlanner = new ProjectileEmissionPlanner();
    private readonly ProjectileSpawner _projectileSpawner = new ProjectileSpawner();

    private EnemyAttackController _attackController;
    private Coroutine _fireRoutine;

    // Windup에서 확정해 실제 발사까지 유지하는 이번 공격의 출력 설정
    private ProjectileAttackActionSO _activeProjectileAction;

    private void Awake()
    {
        _attackController = GetComponent<EnemyAttackController>();
    }

    private void OnDisable()
    {
        StopFireRoutine();
    }

    /// <summary>Projectile 출력 의존성과 유효 Definition 구성 확인</summary>
    public bool CanStart(EnemyAttackContext context)
    {
        // 후보 검사 단계에서는 공유 Action 인스턴스 상태 변경 방지
        ProjectileAttackActionSO action = FindProjectileAction(context.Definition);

        return action != null
            && action.ProjectileDefinition != null
            && action.ProjectileDefinition.projectilePrefabArray != null
            && action.ProjectileDefinition.projectilePrefabArray.Length > 0
            && _muzzle != null;
    }

    /// <summary>이번 공격에 사용할 Projectile 출력 설정 확정</summary>
    public void OnWindupStart(EnemyAttackContext context, TargetSnapshot targetSnapshot)
    {
        // 후보 검사와 실제 발사가 다른 Entry를 사용하지 않도록 시작 경계에서 한 번만 확정
        _activeProjectileAction = FindProjectileAction(context.Definition);
    }

    /// <summary>Controller가 확정한 조준값을 이번 공격에 보관</summary>
    public void OnAimLocked(EnemyAttackContext context, AimSnapshot aimSnapshot)
    {
    }

    /// <summary>고정된 출력 설정과 조준값으로 Projectile 발사 Coroutine 시작</summary>
    public void OnActiveStart(EnemyAttackContext context, AimSnapshot aimSnapshot)
    {
        StopFireRoutine();
        _fireRoutine = StartCoroutine(FireRoutine(context, aimSnapshot, _activeProjectileAction));
    }

    public void OnRecoveryStart(EnemyAttackContext context)
    {
        // Active가 먼저 끝난 경우 남은 예약 발사 중단
        StopFireRoutine();
    }

    public void OnCompleted(EnemyAttackContext context)
    {
        _activeProjectileAction = null;
    }

    public void OnCancelled(EnemyAttackContext context, EnemyAttackCancelReason reason)
    {
        StopFireRoutine();
        _activeProjectileAction = null;
    }

    public void OnActionMarker(EnemyAttackContext context, int markerId)
    {
    }

    // 계획된 발수를 순차 생성하고 마지막 결과를 Controller에 통지
    // 이미 생성된 Projectile은 Action 취소와 무관하게 자체 수명주기 유지
    private IEnumerator FireRoutine(EnemyAttackContext context, AimSnapshot aimSnapshot, ProjectileAttackActionSO action)
    {
        ProjectileEmissionSettings emissionSettings = ProjectileEmissionSettings.FromEnemyAction(action);
        IReadOnlyList<ProjectileEmissionStep> steps = _emissionPlanner.Plan(emissionSettings);

        float baseAimAngle = Mathf.Atan2(aimSnapshot.Direction.y, aimSnapshot.Direction.x) * Mathf.Rad2Deg;
        float[] angleOffsets = EnemyRangedAngleOffsetCalculator.ComputeOffsets(
            action.FirePattern, steps.Count, action.SpreadTotalAngle, action.CircleStartAngle);

        // 발사 승인 시점의 Base와 Stats Modifier로 모든 발이 공유할 Damage Snapshot 고정
        int finalDamage = action.Damage;
        float finalRange = ProjectileFireSnapshotResolver.ResolveRange(context.Attacker, action.Range);
        int finalPierceCount = ProjectileFireSnapshotResolver.ResolvePierceCount(context.Attacker, action.ProjectileDefinition.projectilePierceCount);
        float finalSpeed = ProjectileFireSnapshotResolver.ResolveProjectileSpeed(context.Attacker, action.ProjectileSpeed);
        CombatDamageMath.DamageSnapshot damageSnapshot = CombatDamageMath.CreateSnapshot(context.Attacker);

        bool anySucceeded = false;

        for (int i = 0; i < steps.Count; i++)
        {
            ProjectileEmissionStep step = steps[i];

            ProjectileLaunchRequest request = new ProjectileLaunchRequest(
                step.Definition,
                _muzzle.position,
                baseAimAngle + angleOffsets[i],
                finalSpeed,
                finalDamage,
                finalRange,
                finalPierceCount,
                damageSnapshot,
                attacker: context.Attacker,
                sourceFaction: context.SourceFaction,
                knockbackSpeed: action.KnockbackSpeed,
                knockbackDuration: action.KnockbackDuration);

            ProjectileSpawnResult spawnResult = _projectileSpawner.TrySpawn(step.Prefab, request);

            if (spawnResult.Succeeded)
            {
                anySucceeded = true;
            }
            else
            {
                Logger.LogWarning($"EnemyRangedAttackAction: 투사체 프리팹 '{step.Prefab.name}' 발사 실패({spawnResult.Failure})", this);
            }

            if (i < steps.Count - 1)
            {
                yield return new WaitForSeconds(step.DelayAfter);
            }
        }

        _fireRoutine = null;

        if (anySucceeded)
        {
            _attackController.NotifyActionCompleted();
        }
        else
        {
            _attackController.NotifyActionFailed();
        }
    }

    // 진행 중인 발사 Coroutine과 이번 공격의 캐시 상태 정리
    private void StopFireRoutine()
    {
        if (_fireRoutine != null)
        {
            StopCoroutine(_fireRoutine);
            _fireRoutine = null;
        }
    }

    // OnActiveStart에 연결된 단일 Projectile 출력 탐색
    private static ProjectileAttackActionSO FindProjectileAction(EnemyAttackDefinitionSO definition)
    {
        IReadOnlyList<EnemyAttackActionEntry> entries = definition != null ? definition.Actions : null;

        if (entries == null)
        {
            return null;
        }

        for (int i = 0; i < entries.Count; i++)
        {
            EnemyAttackActionEntry entry = entries[i];

            if (entry.Trigger == EnemyAttackActionTrigger.OnActiveStart && entry.Action is ProjectileAttackActionSO projectileAction)
            {
                return projectileAction;
            }
        }

        return null;
    }
}
