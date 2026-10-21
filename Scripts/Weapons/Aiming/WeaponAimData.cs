using UnityEngine;

/// <summary>
/// 조준 표현과 발사 요청 사이에 전달되는 불변 조준 데이터
/// </summary>
public readonly struct WeaponAimData
{
    public WeaponAimData(AimDirection aimDirection, float aimAngle, float weaponAimAngle, Vector3 weaponDirection, Vector2 playerDirection)
    {
        AimDirection = aimDirection;
        AimAngle = aimAngle;
        WeaponAimAngle = weaponAimAngle;
        WeaponDirection = weaponDirection;
        PlayerDirection = playerDirection;
    }

    public AimDirection AimDirection { get; }
    public float AimAngle { get; }
    public float WeaponAimAngle { get; }
    public Vector3 WeaponDirection { get; }
    public Vector2 PlayerDirection { get; }
}
