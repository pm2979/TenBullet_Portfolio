/// <summary>
/// Gameplay가 카메라 Shake를 요청할 때 사용하는 이벤트 어휘다.
/// 축별 수치와 Cinemachine 의존성은 Camera 계층 내부에 둔다.
/// </summary>
public enum CameraShakeEvent
{
    NormalHit,
    WeakPointHit,
    PlayerDamaged,
    Explosion,
    GroundImpact
}

/// <summary>
/// 카메라 Shake 프로필의 동시 요청 우선순위 어휘다.
/// Gameplay 요청은 AXIS-02부터 <see cref="CameraShakeEvent"/>를 사용한다.
/// </summary>
public enum CameraShakeStrength
{
    Light,
    Medium,
    Heavy
}
