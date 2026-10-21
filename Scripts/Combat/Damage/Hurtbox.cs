using UnityEngine;

/// <summary>
/// 자식 피격 Collider가 부모의 피해 대상을 Projectile에 전달하는 경계 컴포넌트.
/// </summary>
[DisallowMultipleComponent]
public sealed class Hurtbox : MonoBehaviour
{
    private IDamageable _damageable;

    /// <summary>이 Hurtbox가 피해 요청을 전달할 부모 대상이다.</summary>
    public IDamageable Damageable => _damageable;

    private void Awake()
    {
        _damageable = GetComponentInParent<IDamageable>();
    }
}
