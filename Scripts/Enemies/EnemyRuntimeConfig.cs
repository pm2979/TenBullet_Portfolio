/// <summary>Archetype의 AI 행동 설정을 Enemy 한 번의 대여 기간 동안 고정하는 불변 스냅샷</summary>
// 전투 수치는 Stats가 소유하므로 감지·판단·순찰·후퇴 설정만 보관
public sealed class EnemyRuntimeConfig
{
    public bool IsHitInvulnerabilityEnabled { get; }
    public float HitInvulnerabilityDuration { get; }
    public float DetectionRange { get; }
    public float TrackingSightRange { get; }
    public float DetectionInterval { get; }
    public float CombatDecisionInterval { get; }
    public bool UsePatrol { get; }
    public float PatrolRadius { get; }
    public float PatrolIdleDurationMin { get; }
    public float PatrolIdleDurationMax { get; }
    public float PatrolMoveSpeedMultiplier { get; }
    public int PatrolTargetMaxAttempts { get; }
    public bool AllowReposition { get; }
    public float RepositionMaxDuration { get; }
    public float RepositionCooldown { get; }
    public float RepositionMoveSpeedMultiplier { get; }
    public float RepositionDistanceMargin { get; }

    public EnemyRuntimeConfig(EnemyArchetypeSO archetype)
    {
        IsHitInvulnerabilityEnabled = archetype.IsHitInvulnerabilityEnabled;
        HitInvulnerabilityDuration = archetype.HitInvulnerabilityDuration;
        DetectionRange = archetype.DetectionRange;
        TrackingSightRange = archetype.TrackingSightRange > 0f
            ? archetype.TrackingSightRange
            : DetectionRange;
        DetectionInterval = archetype.DetectionInterval;
        CombatDecisionInterval = archetype.CombatDecisionInterval;
        UsePatrol = archetype.UsePatrol;
        PatrolRadius = archetype.PatrolRadius;
        PatrolIdleDurationMin = archetype.PatrolIdleDurationMin;
        PatrolIdleDurationMax = archetype.PatrolIdleDurationMax;
        PatrolMoveSpeedMultiplier = archetype.PatrolMoveSpeedMultiplier;
        PatrolTargetMaxAttempts = archetype.PatrolTargetMaxAttempts;
        AllowReposition = archetype.AllowReposition;
        RepositionMaxDuration = archetype.RepositionMaxDuration;
        RepositionCooldown = archetype.RepositionCooldown;
        RepositionMoveSpeedMultiplier = archetype.RepositionMoveSpeedMultiplier;
        RepositionDistanceMargin = archetype.RepositionDistanceMargin;
    }
}
