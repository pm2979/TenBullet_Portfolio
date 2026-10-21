using System;
using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

/// <summary>
/// 발사 설정을 순서가 고정된 투사체별 생성 계획으로 변환합니다.
/// </summary>
public sealed class ProjectileEmissionPlanner
{
    private readonly Random random;

    public ProjectileEmissionPlanner(Random random = null)
    {
        this.random = random ?? new Random();
    }

    /// <summary>
    /// 발수와 간격을 범위에서 한 번 뽑고 각 탄의 프리팹과 각도 오프셋을 확정합니다.
    /// 사용할 프리팹이 없으면 예외 대신 빈 계획을 반환합니다.
    /// </summary>
    public IReadOnlyList<ProjectileEmissionStep> Plan(ProjectileEmissionSettings settings)
    {
        if (settings.ProjectilePrefabs == null || settings.ProjectilePrefabs.Length == 0)
        {
            Logger.LogWarning("ProjectileEmissionPlanner: ProjectilePrefabs가 비어 있어 발사를 건너뜁니다.");
            return Array.Empty<ProjectileEmissionStep>();
        }

        int spawnCount = NextInt(settings.SpawnCountMin, settings.SpawnCountMax + 1);

        // 단발 계획에는 탄 사이 대기 시간이 없으므로 간격을 0으로 고정합니다.
        float spawnInterval = spawnCount > 1 ? NextFloat(settings.SpawnIntervalMin, settings.SpawnIntervalMax) : 0f;

        float[] angleOffsets = ComputeSpreadOffsets(spawnCount, settings.SpreadAngle);

        List<ProjectileEmissionStep> steps = new List<ProjectileEmissionStep>(spawnCount);

        for (int i = 0; i < spawnCount; i++)
        {
            GameObject prefab = settings.ProjectilePrefabs[NextInt(0, settings.ProjectilePrefabs.Length)];

            steps.Add(new ProjectileEmissionStep(settings.Definition, prefab, spawnInterval, angleOffsets[i]));
        }

        return steps;
    }

    // 중심 방향의 좌우에 탄을 등간격으로 배치해 같은 발수와 분산각에 항상 같은 패턴을 만듭니다.
    private static float[] ComputeSpreadOffsets(int spawnCount, float spreadTotalAngle)
    {
        float[] offsets = new float[spawnCount];

        if (spawnCount <= 1 || spreadTotalAngle <= 0f)
        {
            return offsets;
        }

        float step = spreadTotalAngle / (spawnCount - 1);
        float start = -spreadTotalAngle / 2f;

        for (int i = 0; i < spawnCount; i++)
        {
            offsets[i] = start + step * i;
        }

        return offsets;
    }

    private int NextInt(int minInclusive, int maxExclusive)
    {
        return random.Next(minInclusive, maxExclusive);
    }

    private float NextFloat(float min, float max)
    {
        if (min >= max)
        {
            return min;
        }

        return (float)(min + random.NextDouble() * (max - min));
    }
}
