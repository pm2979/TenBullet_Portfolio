using UnityEngine;

/// <summary>
/// 무기와 소유자 기준의 조준 방향·각도를 함께 전달하는 불변 결과
/// </summary>
public readonly struct AimResult
{
    public readonly Vector2 WeaponDirection;
    public readonly Vector2 PlayerDirection;
    public readonly float WeaponAngleDegrees;
    public readonly float PlayerAngleDegrees;
    public readonly AimDirection AimDirection;

    public AimResult(Vector2 weaponDirection, Vector2 playerDirection, float weaponAngleDegrees, float playerAngleDegrees, AimDirection aimDirection)
    {
        WeaponDirection = weaponDirection;
        PlayerDirection = playerDirection;
        WeaponAngleDegrees = weaponAngleDegrees;
        PlayerAngleDegrees = playerAngleDegrees;
        AimDirection = aimDirection;
    }
}

// 목표 위치 또는 방향 벡터를 무기와 소유자가 공유하는 조준 결과로 변환
public sealed class AimResolver
{
    /// <summary>
    /// 소유자·발사구·목표 위치를 기준으로 각 조준 방향과 각도 계산
    /// </summary>
    public AimResult Resolve(Vector2 playerPosition, Vector2 weaponShootPosition, Vector2 targetPosition)
    {
        Vector2 weaponDirection = targetPosition - weaponShootPosition;
        Vector2 playerDirection = targetPosition - playerPosition;

        float weaponAngleDegrees = GetAngleFromVector(weaponDirection);
        float playerAngleDegrees = GetAngleFromVector(playerDirection);
        AimDirection aimDirection = GetAimDirection(playerAngleDegrees);

        return new AimResult(weaponDirection, playerDirection, weaponAngleDegrees, playerAngleDegrees, aimDirection);
    }

    /// <summary>
    /// 방향 벡터를 소유자와 무기가 동일하게 사용하는 조준 결과로 변환
    /// </summary>
    public AimResult ResolveDirection(Vector2 direction)
    {
        float angleDegrees = GetAngleFromVector(direction);
        AimDirection aimDirection = GetAimDirection(angleDegrees);

        return new AimResult(direction, direction, angleDegrees, angleDegrees, aimDirection);
    }

    // 방향 벡터를 오른쪽 기준 각도로 변환
    private static float GetAngleFromVector(Vector2 vector)
    {
        return Mathf.Atan2(vector.y, vector.x) * Mathf.Rad2Deg;
    }

    // 각도를 상하좌우 네 방향 구간으로 분류
    private static AimDirection GetAimDirection(float angleDegrees)
    {
        if (angleDegrees > 45f && angleDegrees <= 135f) return AimDirection.Up;
        if (angleDegrees > -135f && angleDegrees <= -45f) return AimDirection.Down;
        if (angleDegrees > 135f || angleDegrees <= -135f) return AimDirection.Left;

        return AimDirection.Right;
    }
}
