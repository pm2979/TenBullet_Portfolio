using UnityEngine;

/// <summary>
/// 연속 발사에서 투사체 한 발을 생성하는 데 필요한 불변 계획입니다.
/// </summary>
public readonly struct ProjectileEmissionStep
{
    public ProjectileEmissionStep(
        ProjectileDataSO definition,
        GameObject prefab,
        float delayAfter,
        float angleOffset = 0f)
    {
        Definition = definition;
        Prefab = prefab;
        DelayAfter = delayAfter;
        AngleOffset = angleOffset;
    }

    public ProjectileDataSO Definition { get; }
    public GameObject Prefab { get; }
    public float DelayAfter { get; }

    /// <summary>중심 조준각에 더할 각도입니다. 단위는 도이며 0이면 중심 방향을 유지합니다.</summary>
    public float AngleOffset { get; }
}
