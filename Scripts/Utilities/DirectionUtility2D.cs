using UnityEngine;

/// <summary>
/// 2D 각도-방향 벡터 변환 유틸리티
/// </summary>
public static class DirectionUtility2D
{
    /// <summary>
    /// 60진법 각도(Degree)를 방향 벡터로 변환
    /// 특정 각도로 이동시키거나 발사체를 생성할 때 활용
    /// </summary>
    public static Vector2 DirectionFromDegrees(float angleDegrees)
    {
        return new Vector2(Mathf.Cos(Mathf.Deg2Rad * angleDegrees), Mathf.Sin(Mathf.Deg2Rad * angleDegrees));
    }
}
