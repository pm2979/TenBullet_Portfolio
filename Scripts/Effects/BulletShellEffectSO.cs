using UnityEngine;

// 무기 발사 시 탄피의 배출 방향·탄도·회전·착지 사운드·수명을 정의
[CreateAssetMenu(fileName = "BulletShellEffect_", menuName = "Scriptable Objects/Effects/Bullet Shell Effect")]
public class BulletShellEffectSO : ScriptableObject
{
    [Space(10)]
    [Header("탄피 재생 정보")]

    [Tooltip("PoolManager에 등록되어 대여될 BulletShellView 프리팹")]
    public GameObject bulletShellViewPrefab;
    [Tooltip("탄피가 처음 바닥에 닿을 때 재생할 사운드. 비워두면 재생하지 않는다")]
    public SoundDataSO landingSound;

    [Space(10)]
    [Header("배출 방향")]

    [Tooltip("조준 방향(aimAngle) 기준 탄피 배출 각도 오프셋 - 90이면 조준 방향의 왼쪽, -90이면 오른쪽으로 배출")]
    public float ejectAngleOffsetDegrees = -90f;
    [Tooltip("배출 각도에 매 발사마다 더해지는 무작위 스프레드 범위 (±)")]
    public float ejectAngleRandomSpreadDegrees = 15f;

    [Space(10)]
    [Header("탄도")]

    [Tooltip("지면 방향 배출 속도 최소값")]
    public float ejectSpeedMin = 1.5f;
    [Tooltip("지면 방향 배출 속도 최대값")]
    public float ejectSpeedMax = 2.5f;
    [Tooltip("연출용 높이에 적용할 중력 가속도")]
    public float gravity = 9.8f;
    [Tooltip("배출 순간 위쪽으로 튀어 오르는 초기 속도 최소값")]
    public float initialHeightSpeedMin = 2f;
    [Tooltip("배출 순간 위쪽으로 튀어 오르는 초기 속도 최대값")]
    public float initialHeightSpeedMax = 3f;

    [Space(10)]
    [Header("회전")]

    [Tooltip("한 스텝당 회전할 각도 (0이면 회전하지 않음)")]
    public float rotationStepDegrees = 45f;
    [Tooltip("회전 스텝 사이의 간격(초)")]
    public float rotationStepInterval = 0.03f;

    [Space(10)]
    [Header("생성 팝")]

    [Tooltip("생성 직후 짧게 튀어 오르는 연출용 최대 높이 (0이면 사용하지 않음)")]
    public float spawnPopHeight = 0f;
    [Tooltip("생성 팝 연출 지속 시간 (0이면 사용하지 않음)")]
    public float spawnPopDuration = 0f;

    [Space(10)]
    [Header("수명")]

    [Tooltip("완전히 멈춘 뒤 사라지기 전까지 유지되는 시간(초)")]
    public float lifetime = 3f;
}
