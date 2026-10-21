using System.Collections.Generic;
using UnityEngine;

/// <summary>Enemy Archetype과 공격·Boss Phase 참조의 정적 무결성을 검사</summary>
// 원본 데이터를 수정하지 않고 런타임 실패 가능성을 오류와 경고로 구조화
public static class EnemyArchetypeValidator
{
    /// <summary>단일 Archetype과 연결된 모든 공격 데이터 검증</summary>
    public static EnemyDataValidationResult Validate(EnemyArchetypeSO archetype)
    {
        EnemyDataValidationResult result = new EnemyDataValidationResult();

        ValidateInto(archetype, result);

        return result;
    }

    /// <summary>Archetype 목록의 null·콘텐츠 ID 중복과 각 항목의 내부 데이터 검증</summary>
    public static EnemyDataValidationResult ValidateCollection(IReadOnlyList<EnemyArchetypeSO> archetypes)
    {
        EnemyDataValidationResult result = new EnemyDataValidationResult();

        if (archetypes == null)
        {
            return result;
        }

        Dictionary<string, int> idOccurrences = new Dictionary<string, int>();

        for (int i = 0; i < archetypes.Count; i++)
        {
            EnemyArchetypeSO archetype = archetypes[i];

            if (archetype == null)
            {
                result.AddError($"EnemyArchetypes[{i}]가 null입니다.");
                continue;
            }

            string contentId = archetype.ContentId;

            if (!string.IsNullOrEmpty(contentId))
            {
                idOccurrences.TryGetValue(contentId, out int count);
                idOccurrences[contentId] = count + 1;
            }

            ValidateInto(archetype, result);
        }

        foreach (KeyValuePair<string, int> occurrence in idOccurrences)
        {
            if (occurrence.Value > 1)
            {
                result.AddError($"콘텐츠 ID '{occurrence.Key}'가 Archetype {occurrence.Value}개에서 중복 사용되었습니다.");
            }
        }

        return result;
    }

    private static void ValidateInto(EnemyArchetypeSO archetype, EnemyDataValidationResult result)
    {
        if (archetype == null)
        {
            result.AddError("Archetype이 null입니다.");
            return;
        }

        if (string.IsNullOrEmpty(archetype.ContentId))
        {
            result.AddError($"'{archetype.name}'의 콘텐츠 ID가 비어 있습니다.", archetype);
        }

        ValidateBehaviorSettings(archetype, result);
        ValidateReposition(archetype, result);

        HashSet<EnemyAttackDefinitionSO> catalogSet = BuildCatalogSet(archetype, result);

        ValidatePatrol(archetype, result);
        ValidateInitiallyUnlockedPatterns(archetype, catalogSet, result);
        ValidateBossPhases(archetype, catalogSet, result);
        ValidateRecommendedPatternCount(archetype, result);
    }

    // 기존 Asset의 0은 폴백 값으로 허용하고 명시값은 탐지 거리 이상으로 제한
    private static void ValidateTrackingSightRange(EnemyArchetypeSO archetype, EnemyDataValidationResult result)
    {
        if (archetype.TrackingSightRange == 0f)
        {
            result.AddWarning(
                $"'{archetype.name}'의 trackingSightRange가 설정되지 않아 detectionRange({archetype.DetectionRange})로 폴백합니다. Inspector에서 명시값으로 마이그레이션해야 합니다.",
                archetype);
        }
        else if (archetype.TrackingSightRange < archetype.DetectionRange)
        {
            result.AddError(
                $"'{archetype.name}'의 trackingSightRange({archetype.TrackingSightRange})는 detectionRange({archetype.DetectionRange})보다 작을 수 없습니다.",
                archetype);
        }
    }

    // 최대 공격 범위가 추적 시야를 넘는 실행 불가능 구간 경고
    private static void ValidateAttackTrackingRange(
        EnemyArchetypeSO archetype, EnemyAttackDefinitionSO attack, EnemyDataValidationResult result)
    {
        float trackingSightRange = archetype.TrackingSightRange > 0f
            ? archetype.TrackingSightRange
            : archetype.DetectionRange;

        if (attack.MaxRange > trackingSightRange)
        {
            result.AddWarning(
                $"'{archetype.name}'의 공격 '{attack.name}' maxRange({attack.MaxRange})가 trackingSightRange({trackingSightRange})보다 커 바깥 구간에서는 공격할 수 없습니다.",
                attack);
        }
    }

    // 후퇴를 사용하는 Archetype에 한해 관련 수치 검증
    private static void ValidateReposition(EnemyArchetypeSO archetype, EnemyDataValidationResult result)
    {
        if (!archetype.AllowReposition)
        {
            return;
        }

        if (archetype.RepositionMaxDuration <= 0f)
        {
            result.AddError($"'{archetype.name}'은(는) AllowReposition이지만 repositionMaxDuration이 {archetype.RepositionMaxDuration}입니다.", archetype);
        }

        if (archetype.RepositionMoveSpeedMultiplier <= 0f)
        {
            result.AddError($"'{archetype.name}'은(는) AllowReposition이지만 repositionMoveSpeedMultiplier가 {archetype.RepositionMoveSpeedMultiplier}입니다.", archetype);
        }

        if (archetype.RepositionCooldown < 0f)
        {
            result.AddError($"'{archetype.name}'의 repositionCooldown은 음수일 수 없는데 {archetype.RepositionCooldown}입니다.", archetype);
        }

        if (archetype.RepositionDistanceMargin < 0f)
        {
            result.AddError($"'{archetype.name}'의 repositionDistanceMargin은 음수일 수 없는데 {archetype.RepositionDistanceMargin}입니다.", archetype);
        }
    }

    // Patrol 활성 Archetype에 한해 실제 소비되는 순찰 수치 검증
    private static void ValidatePatrol(EnemyArchetypeSO archetype, EnemyDataValidationResult result)
    {
        if (!archetype.UsePatrol)
        {
            return;
        }

        if (archetype.PatrolRadius <= 0f)
        {
            result.AddError($"'{archetype.name}'은(는) UsePatrol이지만 patrolRadius가 {archetype.PatrolRadius}입니다.", archetype);
        }

        if (archetype.PatrolMoveSpeedMultiplier <= 0f)
        {
            result.AddError($"'{archetype.name}'은(는) UsePatrol이지만 patrolMoveSpeedMultiplier가 {archetype.PatrolMoveSpeedMultiplier}입니다.", archetype);
        }

        if (archetype.PatrolIdleDurationMin < 0f)
        {
            result.AddError($"'{archetype.name}'의 patrolIdleDurationMin은 음수일 수 없는데 {archetype.PatrolIdleDurationMin}입니다.", archetype);
        }

        if (archetype.PatrolIdleDurationMax < archetype.PatrolIdleDurationMin)
        {
            result.AddError(
                $"'{archetype.name}'의 patrolIdleDurationMax({archetype.PatrolIdleDurationMax})는 patrolIdleDurationMin({archetype.PatrolIdleDurationMin})보다 작을 수 없습니다.",
                archetype);
        }

        if (archetype.PatrolTargetMaxAttempts <= 0)
        {
            result.AddError($"'{archetype.name}'의 patrolTargetMaxAttempts는 0보다 커야 하는데 {archetype.PatrolTargetMaxAttempts}입니다.", archetype);
        }
    }

    // 감지 범위·판단 주기·피격 무적 시간의 유효 범위 검증
    private static void ValidateBehaviorSettings(EnemyArchetypeSO archetype, EnemyDataValidationResult result)
    {
        if (archetype.DetectionRange <= 0f)
        {
            result.AddError($"'{archetype.name}'의 detectionRange는 0보다 커야 하는데 {archetype.DetectionRange}입니다.", archetype);
        }

        ValidateTrackingSightRange(archetype, result);

        if (archetype.DetectionInterval <= 0f)
        {
            result.AddError($"'{archetype.name}'의 detectionInterval은 0보다 커야 하는데 {archetype.DetectionInterval}입니다.", archetype);
        }

        if (archetype.CombatDecisionInterval <= 0f)
        {
            result.AddError($"'{archetype.name}'의 combatDecisionInterval은 0보다 커야 하는데 {archetype.CombatDecisionInterval}입니다.", archetype);
        }

        if (archetype.DetectionInterval < archetype.CombatDecisionInterval)
        {
            result.AddWarning(
                $"'{archetype.name}'의 detectionInterval({archetype.DetectionInterval})이 combatDecisionInterval({archetype.CombatDecisionInterval})보다 짧습니다.",
                archetype);
        }

        if (archetype.HitInvulnerabilityDuration < 0f)
        {
            result.AddError($"'{archetype.name}'의 hitInvulnerabilityDuration은 음수일 수 없는데 {archetype.HitInvulnerabilityDuration}입니다.", archetype);
        }
        else if (archetype.IsHitInvulnerabilityEnabled && archetype.HitInvulnerabilityDuration <= 0f)
        {
            result.AddError($"'{archetype.name}'은(는) 피격 무적이 활성화되어 있지만 hitInvulnerabilityDuration이 {archetype.HitInvulnerabilityDuration}입니다.", archetype);
        }

        if (archetype.EncounterCost <= 0)
        {
            result.AddError($"'{archetype.name}'의 encounterCost는 0보다 커야 하는데 {archetype.EncounterCost}입니다.", archetype);
        }
    }

    // 카탈로그의 null·중복·공격 데이터를 검증하고 참조 판별용 집합 생성
    private static HashSet<EnemyAttackDefinitionSO> BuildCatalogSet(EnemyArchetypeSO archetype, EnemyDataValidationResult result)
    {
        HashSet<EnemyAttackDefinitionSO> catalogSet = new HashSet<EnemyAttackDefinitionSO>();
        IReadOnlyList<EnemyAttackDefinitionSO> catalog = archetype.AttackCatalog;

        if (catalog == null)
        {
            return catalogSet;
        }

        for (int i = 0; i < catalog.Count; i++)
        {
            EnemyAttackDefinitionSO attack = catalog[i];

            if (attack == null)
            {
                result.AddError($"'{archetype.name}'의 attackCatalog[{i}]가 null입니다.", archetype);
                continue;
            }

            if (!catalogSet.Add(attack))
            {
                result.AddError($"'{archetype.name}'의 attackCatalog에 '{attack.name}'이(가) 중복 등록되어 있습니다.", archetype);
                continue;
            }

            ValidateAttackDefinition(attack, result);
            ValidateAttackTrackingRange(archetype, attack, result);
        }

        return catalogSet;
    }

    // 모든 공격이 공유하는 거리·시간·쿨다운과 Projectile 출력 검증
    private static void ValidateAttackDefinition(EnemyAttackDefinitionSO attack, EnemyDataValidationResult result)
    {
        string label = $"'{attack.name}'";

        if (attack.MinRange < 0f)
        {
            result.AddError($"{label}의 minRange는 음수일 수 없는데 {attack.MinRange}입니다.", attack);
        }

        if (attack.MaxRange < 0f)
        {
            result.AddError($"{label}의 maxRange는 음수일 수 없는데 {attack.MaxRange}입니다.", attack);
        }

        if (attack.MinRange > attack.MaxRange)
        {
            result.AddError($"{label}의 minRange({attack.MinRange})가 maxRange({attack.MaxRange})보다 큽니다.", attack);
        }

        ValidateNonNegative(attack.WindupDuration, $"{label}의 windupDuration", attack, result);
        ValidateNonNegative(attack.ActiveDuration, $"{label}의 activeDuration", attack, result);
        ValidateNonNegative(attack.RecoveryDuration, $"{label}의 recoveryDuration", attack, result);
        ValidateNonNegative(attack.SafeTimeout, $"{label}의 safeTimeout", attack, result);
        ValidateNonNegative(attack.Cooldown, $"{label}의 cooldown", attack, result);

        ValidateAdvanceModes(attack, label, result);
        ValidateRangedOutput(attack, label, result);
    }

    // 신호 기반 단계의 무한 대기 방지를 위한 SafeTimeout과 조합 제한 검증
    private static void ValidateAdvanceModes(EnemyAttackDefinitionSO attack, string label, EnemyDataValidationResult result)
    {
        if (attack.WindupAdvanceMode == EnemyAttackAdvanceMode.ActionCompleted)
        {
            result.AddError($"{label}의 windupAdvanceMode는 ActionCompleted를 지원하지 않습니다. Timer 또는 Marker를 사용하세요.", attack);
        }

        bool waitsForSignal =
            attack.WindupAdvanceMode != EnemyAttackAdvanceMode.Timer ||
            attack.ActiveAdvanceMode != EnemyAttackAdvanceMode.Timer;

        if (waitsForSignal && attack.SafeTimeout <= 0f)
        {
            result.AddError($"{label}은(는) 신호를 기다리는 진행 방식을 사용하므로 safeTimeout이 0보다 커야 하는데 {attack.SafeTimeout}입니다.", attack);
        }
    }

    private static void ValidateNonNegative(float value, string label, Object context, EnemyDataValidationResult result)
    {
        if (value < 0f)
        {
            result.AddError($"{label}은(는) 음수일 수 없는데 {value}입니다.", context);
        }
    }

    // Projectile 생성 계약을 위반하는 Prefab·범위·속도·관통·개수 값 검증
    private static void ValidateProjectileDefinition(ProjectileAttackActionSO action, string label, Object context, EnemyDataValidationResult result)
    {
        ProjectileDataSO projectileDefinition = action.ProjectileDefinition;

        if (projectileDefinition == null)
        {
            result.AddError($"{label}이(가) 비어 있습니다.", context);
            return;
        }

        GameObject[] prefabs = projectileDefinition.projectilePrefabArray;

        if (prefabs == null || prefabs.Length == 0)
        {
            result.AddError($"{label}('{projectileDefinition.name}')의 projectilePrefabArray가 비어 있습니다.", context);
        }
        else
        {
            for (int i = 0; i < prefabs.Length; i++)
            {
                if (prefabs[i] == null)
                {
                    result.AddError($"{label}('{projectileDefinition.name}')의 projectilePrefabArray[{i}]가 null입니다.", context);
                }
            }
        }

        if (action.Range <= 0f)
        {
            result.AddError($"{label}('{projectileDefinition.name}')의 Range는 0보다 커야 하는데 {action.Range}입니다.", context);
        }

        if (action.ProjectileSpeed < 0f)
        {
            result.AddError($"{label}('{projectileDefinition.name}')의 ProjectileSpeed는 음수일 수 없는데 {action.ProjectileSpeed}입니다.", context);
        }

        if (projectileDefinition.projectilePierceCount < 0)
        {
            result.AddError($"{label}('{projectileDefinition.name}')의 projectilePierceCount는 음수일 수 없는데 {projectileDefinition.projectilePierceCount}입니다.", context);
        }

        if (action.SpawnCountMin < 1)
        {
            result.AddError($"{label}('{projectileDefinition.name}')의 SpawnCountMin은 1 이상이어야 하는데 {action.SpawnCountMin}입니다.", context);
        }

        if (action.SpawnCountMin > action.SpawnCountMax)
        {
            result.AddError($"{label}('{projectileDefinition.name}')의 SpawnCountMin({action.SpawnCountMin})이 SpawnCountMax({action.SpawnCountMax})보다 큽니다.", context);
        }

        if (action.SpawnIntervalMin < 0f)
        {
            result.AddError($"{label}('{projectileDefinition.name}')의 SpawnIntervalMin은 0 이상이어야 하는데 {action.SpawnIntervalMin}입니다.", context);
        }

        if (action.SpawnIntervalMin > action.SpawnIntervalMax)
        {
            result.AddError($"{label}('{projectileDefinition.name}')의 SpawnIntervalMin({action.SpawnIntervalMin})이 SpawnIntervalMax({action.SpawnIntervalMax})보다 큽니다.", context);
        }
    }

    // Ranged Action이 소비하는 단일 OnActiveStart Projectile 출력 계약 검증
    private static void ValidateRangedOutput(EnemyAttackDefinitionSO attack, string label, EnemyDataValidationResult result)
    {
        ProjectileAttackActionSO action = FindAction<ProjectileAttackActionSO>(
            attack, EnemyAttackActionTrigger.OnActiveStart, out int matchCount);

        if (matchCount > 1)
        {
            result.AddError($"{label}(Ranged)에 OnActiveStart ProjectileAttackActionSO Entry가 {matchCount}개 있습니다. 하나만 두세요.", attack);
        }

        if (action == null)
        {
            result.AddError($"{label}(Ranged)의 ProjectileAttackActionSO Entry(OnActiveStart)가 없습니다.", attack);
            return;
        }

        // 여러 발 출력이 끝까지 실행되도록 ActionCompleted 진행 방식 요구
        if (action.SpawnCountMax > 1 && attack.ActiveAdvanceMode != EnemyAttackAdvanceMode.ActionCompleted)
        {
            result.AddError($"{label}(Ranged)은(는) 여러 발(spawnCountMax {action.SpawnCountMax})을 발사하므로 activeAdvanceMode가 ActionCompleted여야 합니다.", attack);
        }

        ValidateProjectileDefinition(action, $"{label}(Ranged)의 projectileDefinition", attack, result);
    }

    // Runtime이 소비할 첫 OnActiveStart Entry와 중복 검사용 전체 개수 탐색
    private static T FindAction<T>(
        EnemyAttackDefinitionSO attack, EnemyAttackActionTrigger trigger, out int matchCount) where T : EnemyAttackActionSO
    {
        matchCount = 0;

        IReadOnlyList<EnemyAttackActionEntry> entries = attack.Actions;

        if (entries == null)
        {
            return null;
        }

        T firstMatch = null;

        for (int i = 0; i < entries.Count; i++)
        {
            EnemyAttackActionEntry entry = entries[i];

            if (entry.Trigger != trigger || !(entry.Action is T typed))
            {
                continue;
            }

            matchCount++;

            if (firstMatch == null)
            {
                firstMatch = typed;
            }
        }

        return firstMatch;
    }

    // 최초 해금 목록의 null과 카탈로그 외부 참조 검증
    private static void ValidateInitiallyUnlockedPatterns(EnemyArchetypeSO archetype, HashSet<EnemyAttackDefinitionSO> catalogSet, EnemyDataValidationResult result)
    {
        IReadOnlyList<EnemyAttackDefinitionSO> patterns = archetype.InitiallyUnlockedPatterns;

        if (patterns == null)
        {
            return;
        }

        for (int i = 0; i < patterns.Count; i++)
        {
            EnemyAttackDefinitionSO pattern = patterns[i];

            if (pattern == null)
            {
                result.AddError($"'{archetype.name}'의 initiallyUnlockedPatterns[{i}]가 null입니다.", archetype);
                continue;
            }

            if (!catalogSet.Contains(pattern))
            {
                result.AddError($"'{archetype.name}'의 initiallyUnlockedPatterns에 attackCatalog 밖의 '{pattern.name}'이(가) 있습니다.", archetype);
            }
        }
    }

    // Boss Phase 임계값 순서·카탈로그 참조·최초 공격 후보 검증
    private static void ValidateBossPhases(EnemyArchetypeSO archetype, HashSet<EnemyAttackDefinitionSO> catalogSet, EnemyDataValidationResult result)
    {
        IReadOnlyList<BossPhaseDefinition> phases = archetype.BossPhases;
        int phaseCount = phases?.Count ?? 0;
        bool isBoss = archetype.MonsterGrade == MonsterGrade.Boss;

        if (!isBoss && phaseCount > 0)
        {
            result.AddWarning($"'{archetype.name}'은(는) Boss가 아니지만 Boss Phase가 설정되어 있습니다.", archetype);
        }

        if (isBoss && phaseCount == 0)
        {
            result.AddError($"'{archetype.name}'은(는) Boss이지만 Phase 1이 없습니다.", archetype);
        }

        BossPhaseDefinition previousPhase = null;

        for (int i = 0; i < phaseCount; i++)
        {
            BossPhaseDefinition phase = phases[i];

            if (phase == null)
            {
                result.AddError($"'{archetype.name}'의 bossPhases[{i}]가 null입니다.", archetype);
                continue;
            }

            ValidatePhaseThreshold(archetype, phase, previousPhase, i, result);
            ValidatePhaseCatalogReferences(archetype, phase, i, catalogSet, result);

            previousPhase = phase;
        }

        if (isBoss)
        {
            bool hasInitialCandidate = HasAnyNonNull(archetype.InitiallyUnlockedPatterns);

            if (!hasInitialCandidate && phaseCount > 0 && phases[0] != null)
            {
                hasInitialCandidate = HasAnyNonNull(phases[0].UnlockPatterns);
            }

            if (!hasInitialCandidate)
            {
                result.AddError($"'{archetype.name}'의 최초 공격 후보가 완전히 비어 있습니다.", archetype);
            }
        }
    }

    private static void ValidatePhaseThreshold(EnemyArchetypeSO archetype, BossPhaseDefinition phase, BossPhaseDefinition previousPhase, int index, EnemyDataValidationResult result)
    {
        float ratio = phase.EnterAtOrBelowHealthRatio;

        if (index == 0)
        {
            if (!Mathf.Approximately(ratio, 1f))
            {
                result.AddError($"'{archetype.name}'의 Phase 1 임계값은 1.0이어야 하는데 {ratio}입니다.", archetype);
            }

            return;
        }

        if (ratio <= 0f || ratio >= 1f)
        {
            result.AddError($"'{archetype.name}'의 Phase {index + 1} 임계값 {ratio}이(가) 0~1 범위를 벗어났습니다.", archetype);
        }

        if (previousPhase == null)
        {
            return;
        }

        float previousRatio = previousPhase.EnterAtOrBelowHealthRatio;

        if (ratio >= previousRatio)
        {
            result.AddError($"'{archetype.name}'의 Phase {index + 1} 임계값 {ratio}이(가) 이전 Phase({previousRatio}) 이상입니다. 엄격한 내림차순이어야 합니다.", archetype);
        }
    }

    private static void ValidatePhaseCatalogReferences(EnemyArchetypeSO archetype, BossPhaseDefinition phase, int index, HashSet<EnemyAttackDefinitionSO> catalogSet, EnemyDataValidationResult result)
    {
        EnemyAttackDefinitionSO entryPattern = phase.EntryPattern;

        if (entryPattern != null && !catalogSet.Contains(entryPattern))
        {
            result.AddError($"'{archetype.name}'의 Phase {index + 1} entryPattern '{entryPattern.name}'이(가) attackCatalog 밖입니다.", archetype);
        }

        IReadOnlyList<EnemyAttackDefinitionSO> unlockPatterns = phase.UnlockPatterns;

        if (unlockPatterns == null)
        {
            return;
        }

        HashSet<EnemyAttackDefinitionSO> seenInPhase = new HashSet<EnemyAttackDefinitionSO>();

        for (int i = 0; i < unlockPatterns.Count; i++)
        {
            EnemyAttackDefinitionSO pattern = unlockPatterns[i];

            if (pattern == null)
            {
                result.AddError($"'{archetype.name}'의 Phase {index + 1} unlockPatterns[{i}]가 null입니다.", archetype);
                continue;
            }

            if (!catalogSet.Contains(pattern))
            {
                result.AddError($"'{archetype.name}'의 Phase {index + 1} unlockPatterns에 attackCatalog 밖의 '{pattern.name}'이(가) 있습니다.", archetype);
            }

            if (!seenInPhase.Add(pattern))
            {
                result.AddWarning($"'{archetype.name}'의 Phase {index + 1} unlockPatterns에 '{pattern.name}'이(가) 중복되어 있습니다.", archetype);
            }
        }
    }

    private static bool HasAnyNonNull(IReadOnlyList<EnemyAttackDefinitionSO> patterns)
    {
        if (patterns == null)
        {
            return false;
        }

        for (int i = 0; i < patterns.Count; i++)
        {
            if (patterns[i] != null)
            {
                return true;
            }
        }

        return false;
    }

    // 등급별 권장 패턴 수 차이는 실행을 막지 않는 경고로만 보고
    private static void ValidateRecommendedPatternCount(EnemyArchetypeSO archetype, EnemyDataValidationResult result)
    {
        int initialCount = archetype.InitiallyUnlockedPatterns?.Count ?? 0;

        switch (archetype.MonsterGrade)
        {
            case MonsterGrade.Normal:
                if (initialCount != 1)
                {
                    result.AddWarning($"'{archetype.name}'(Normal)의 initiallyUnlockedPatterns 권장 개수는 1개인데 {initialCount}개입니다.", archetype);
                }
                break;

            case MonsterGrade.Elite:
                if (initialCount != 2)
                {
                    result.AddWarning($"'{archetype.name}'(Elite)의 initiallyUnlockedPatterns 권장 개수는 2개인데 {initialCount}개입니다.", archetype);
                }
                break;

            case MonsterGrade.Boss:
                if (initialCount < 2)
                {
                    result.AddWarning($"'{archetype.name}'(Boss)의 initiallyUnlockedPatterns 권장 개수는 2개 이상인데 {initialCount}개입니다.", archetype);
                }
                break;
        }
    }
}
