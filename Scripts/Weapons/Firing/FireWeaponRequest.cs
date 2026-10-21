/// <summary>
/// 입력 상태와 조준 결과를 함께 전달하는 불변 발사 요청
/// </summary>
public readonly struct FireWeaponRequest
{
    public FireWeaponRequest(bool isFiring, bool firePreviousFrame, WeaponAimData weaponAimData)
    {
        IsFiring = isFiring;
        FirePreviousFrame = firePreviousFrame;
        WeaponAimData = weaponAimData;
    }

    public bool IsFiring { get; }
    public bool FirePreviousFrame { get; }
    public WeaponAimData WeaponAimData { get; }
}
