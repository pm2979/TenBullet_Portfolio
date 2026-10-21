/// <summary>원거리 발사 패턴별 조준 기준 각도 Offset을 계산하는 순수 계산기</summary>
public static class EnemyRangedAngleOffsetCalculator
{
    /// <summary>발사 패턴과 발수에 맞는 각 발의 조준 기준 Offset 배열 계산</summary>
    public static float[] ComputeOffsets(EnemyRangedFirePattern pattern, int count, float spreadTotalAngle, float circleStartAngle)
    {
        float[] offsets = new float[count];

        switch (pattern)
        {
            case EnemyRangedFirePattern.Spread:
                ComputeSpreadOffsets(offsets, spreadTotalAngle);
                break;

            case EnemyRangedFirePattern.Circle:
                ComputeCircleOffsets(offsets, circleStartAngle);
                break;

            default:
                // Single과 Burst는 모든 발을 조준 방향에 유지
                break;
        }

        return offsets;
    }

    // 전체 각도 안에서 조준 방향을 중심으로 대칭 분산
    private static void ComputeSpreadOffsets(float[] offsets, float spreadTotalAngle)
    {
        int count = offsets.Length;

        if (count <= 1)
        {
            return;
        }

        float step = spreadTotalAngle / (count - 1);
        float start = -spreadTotalAngle / 2f;

        for (int i = 0; i < count; i++)
        {
            offsets[i] = start + step * i;
        }
    }

    // 시작 각도부터 한 바퀴를 발수만큼 균등 분할
    private static void ComputeCircleOffsets(float[] offsets, float circleStartAngle)
    {
        int count = offsets.Length;

        if (count <= 0)
        {
            return;
        }

        float step = 360f / count;

        for (int i = 0; i < count; i++)
        {
            offsets[i] = circleStartAngle + step * i;
        }
    }
}
