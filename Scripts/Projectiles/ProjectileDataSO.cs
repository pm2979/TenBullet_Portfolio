using UnityEngine;

[CreateAssetMenu(fileName = "ProjectileData_", menuName = "Scriptable Objects/Weapons/ProjectileData")]
// 투사체 프리팹과 충전·관통·궤적·적중 연출처럼 투사체 자체의 콘텐츠 설정을 보관합니다.
public class ProjectileDataSO : ScriptableObject
{
    [Space(5)]
    [Header("투사체 스프라이트 및 프리팹")]
    [Tooltip("투사체 스프라이트")]
    public Sprite projectileSprite;
    [Tooltip("투사체 프리팹")]
    public GameObject[] projectilePrefabArray;

    [Space(5)]
    [Tooltip("충돌 위치 히트 이펙트")]
    public EffectDefinitionSO projectileHitSpriteEffect;

    [Space(5)]
    [Header("투사체 파라미터")]
    [Tooltip("투사체가 이동 전에 충전되는 시간(초)")]
    public float projectileChargeTime = 0.1f;
    [Tooltip("첫 적대 대상 이후 추가로 관통 가능한 고유 적대 대상 수. 0이면 최대 1개 대상만 처리한다. RuntimeStats Modifier 적용 전 콘텐츠 Base다")]
    public int projectilePierceCount = 0;

    [Space(5)]
    [Header("투사체 궤적 정보")]
    [Tooltip("투사체 궤적 선택 - 선택 시 나머지 투사체 궤적 값들을 채워야 한다.")]
    public bool isProjectileTrail = false;
    [Tooltip("투사체 궤적 지속 시간(초)")]
    public float projectileTrailTime = 3f;
    [Tooltip("투사체 궤적의 시작 너비")]
    [Range(0f, 1f)] public float projectileTrailStartWidth;
    [Tooltip("투사체 궤적의 끝 너비")]
    [Range(0f, 1f)] public float projectileTrailEndWidth;

    private void OnValidate()
    {
        if (projectilePierceCount < 0)
        {
            projectilePierceCount = 0;
        }
    }
}
