using UnityEngine;

/// <summary>
/// 방향·속도로 리지드바디 속도를 설정하는 물리 실행 전용 클래스
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[DisallowMultipleComponent]
public class VelocityMotor2D : MonoBehaviour
{
    private Rigidbody2D _rigidBody2D;

    private void Awake()
    {
        _rigidBody2D = GetComponent<Rigidbody2D>();
    }

    /// <summary>
    /// 방향·속도로 리지드바디 속도를 설정한다
    /// </summary>
    public void SetVelocity(Vector2 direction, float speed)
    {
        _rigidBody2D.linearVelocity = direction * speed;
    }

    /// <summary>
    /// 리지드바디 속도를 0으로 만든다
    /// </summary>
    public void Stop()
    {
        _rigidBody2D.linearVelocity = Vector2.zero;
    }
}
