using UnityEngine;

/// <summary>투사체 공격의 발사 패턴·전투 수치·생성 횟수와 간격을 정의</summary>
[CreateAssetMenu(fileName = "ProjectileAttackAction_", menuName = "Scriptable Objects/Enemies/Actions/ProjectileAttackAction")]
public sealed class ProjectileAttackActionSO : EnemyAttackActionSO
{
    [Space(5)]
    [Header("투사체")]
    [Tooltip("발사할 투사체 정의. 프리팹 선택은 이 정의를 그대로 사용한다")]
    [SerializeField] private ProjectileDataSO _projectileDefinition;
    [Tooltip("발사 각도 패턴. Single/Burst는 조준 방향 그대로 발사하고, Spread는 대칭 분산, Circle은 균등 원형 분산으로 발사한다")]
    [SerializeField] private EnemyRangedFirePattern _firePattern = EnemyRangedFirePattern.Single;
    [Tooltip("Spread 패턴에서 전체 탄이 이루는 총 각도(도). 예: 30이면 조준 방향 기준 좌우 15도씩 분산한다")]
    [SerializeField] private float _spreadTotalAngle;
    [Tooltip("Circle 패턴에서 첫 탄이 시작하는 각도(도, 조준 방향 기준 오프셋)")]
    [SerializeField] private float _circleStartAngle;

    [Space(5)]
    [Header("투사체 발사 Base 값")]
    [Tooltip("이 Action이 사용할 피해량 Base")]
    [SerializeField] private int _damage = 1;
    [Tooltip("이 Action이 사용할 사거리 Base")]
    [SerializeField] private float _range = 1f;
    [Tooltip("이 Action이 사용할 투사체 이동 속도 Base")]
    [SerializeField] private float _projectileSpeed = 5f;
    [Tooltip("적중 시 적용할 넉백 속도. 0 이하면 넉백을 적용하지 않는다")]
    [SerializeField] private float _knockbackSpeed = 0f;
    [Tooltip("넉백이 유지되는 시간(초). 0 이하면 넉백을 적용하지 않는다")]
    [SerializeField] private float _knockbackDuration = 0f;

    [Space(5)]
    [Header("투사체 생성 설정")]
    [Tooltip("공격 1회당 생성할 투사체 최소 개수")]
    [SerializeField] private int _spawnCountMin = 1;
    [Tooltip("공격 1회당 생성할 투사체 최대 개수")]
    [SerializeField] private int _spawnCountMax = 1;
    [Tooltip("투사체 생성 최소 간격(초)")]
    [SerializeField] private float _spawnIntervalMin = 0f;
    [Tooltip("투사체 생성 최대 간격(초)")]
    [SerializeField] private float _spawnIntervalMax = 0f;

    public ProjectileDataSO ProjectileDefinition => _projectileDefinition;
    public EnemyRangedFirePattern FirePattern => _firePattern;
    public float SpreadTotalAngle => _spreadTotalAngle;
    public float CircleStartAngle => _circleStartAngle;
    public int Damage => _damage;
    public float Range => _range;
    public float ProjectileSpeed => _projectileSpeed;
    public float KnockbackSpeed => _knockbackSpeed;
    public float KnockbackDuration => _knockbackDuration;
    public int SpawnCountMin => _spawnCountMin;
    public int SpawnCountMax => _spawnCountMax;
    public float SpawnIntervalMin => _spawnIntervalMin;
    public float SpawnIntervalMax => _spawnIntervalMax;
}
