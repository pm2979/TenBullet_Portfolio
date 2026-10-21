using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 장착 무기의 스프라이트·충돌 영역·발사 기준점을 동기화
/// </summary>
[DisallowMultipleComponent]
public class ActiveWeapon : MonoBehaviour
{
    [SerializeField] private SpriteRenderer _weaponSpriteRenderer;

    [Tooltip("시각 반동 전용 Transform, 발사·효과·탄피 위치와 콜라이더는 자식으로 배치하지 않음")]
    [SerializeField] private Transform _weaponVisualTransform;

    [SerializeField] private PolygonCollider2D _weaponCollider;
    
    [SerializeField] private Transform _shootPoint;
    
    [SerializeField] private Transform _effectPoint;

    [SerializeField] private Transform _shellEjectPoint;

    private Weapon weapon;

    /// <summary>
    /// 현재 무기를 교체하고 스프라이트·충돌 영역·발사 위치 동기화
    /// </summary>
    public void SetWeapon(Weapon weapon)
    {
        this.weapon = weapon;

        ApplyWeaponSprite();
        SyncColliderShapeToSprite();
        UpdateShootPosition();
    }

    // 장착 무기의 스프라이트 적용
    private void ApplyWeaponSprite()
    {
        _weaponSpriteRenderer.sprite = weapon.weaponDetails.weaponSprite;
    }

    // 스프라이트의 첫 Physics Shape를 무기 충돌 영역으로 동기화
    private void SyncColliderShapeToSprite()
    {
        if (_weaponCollider == null || _weaponSpriteRenderer.sprite == null)
        {
            return;
        }

        List<Vector2> spritePhysicsShapePointsList = new List<Vector2>();
        _weaponSpriteRenderer.sprite.GetPhysicsShape(0, spritePhysicsShapePointsList);

        _weaponCollider.points = spritePhysicsShapePointsList.ToArray();
    }

    // 무기 데이터의 로컬 발사 위치 적용
    private void UpdateShootPosition()
    {
        _shootPoint.localPosition = weapon.weaponDetails.weaponShootPosition;
    }

    public Weapon GetCurrentWeapon()
    {
        return weapon;
    }

    public Vector3 GetShootPosition()
    {
        return _shootPoint.position;
    }

    public Vector3 GetShootEffectPosition()
    {
        return _effectPoint.position;
    }

    public Vector3 GetShellEjectPosition()
    {
        return _shellEjectPoint.position;
    }

    public Transform GetWeaponEffectPositionTransform()
    {
        return _effectPoint;
    }

    public Collider2D GetWeaponCollider()
    {
        return _weaponCollider;
    }

    public Transform GetWeaponVisualTransform()
    {
        return _weaponVisualTransform;
    }
}
