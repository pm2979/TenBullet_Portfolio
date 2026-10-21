using UnityEngine;

/// <summary>
/// 목표 위치 방향으로 리지드바디를 이동시키는 물리 실행 전용 클래스
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[DisallowMultipleComponent]
public class PositionMotor2D : MonoBehaviour
{
    private Rigidbody2D _rigidBody2D;

    private void Awake()
    {
        _rigidBody2D = GetComponent<Rigidbody2D>();
    }

    /// <summary>
    /// 현재 Rigidbody2D 위치에서 목표 위치까지 속도 기반으로 한 FixedUpdate 스텝 이동한다.
    /// 목표를 넘지 않도록 이동량을 남은 거리 이하로 제한한다
    /// </summary>
    public void MoveTowards(Vector2 targetPosition, float speed)
    {
        float maxDistanceDelta = speed * Time.fixedDeltaTime;
        Vector2 nextPosition = Vector2.MoveTowards(_rigidBody2D.position, targetPosition, maxDistanceDelta);
        _rigidBody2D.MovePosition(nextPosition);
    }
}
