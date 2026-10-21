using UnityEngine;

// 플립북 이펙트의 프리팹·프레임·재생 속도·표시 정보를 보관
[CreateAssetMenu(fileName = "EffectDefinition_", menuName = "Scriptable Objects/Effects/Effect Definition")]
public class EffectDefinitionSO : ScriptableObject
{
    [Space(10)]
    [Header("이펙트 재생 정보")]

    [Tooltip("PoolManager에 등록되어 대여될 EffectView 프리팹")]
    public GameObject effectViewPrefab;

    [Tooltip("순서대로 재생할 스프라이트 프레임 배열")]
    public Sprite[] frames;

    [Tooltip("초당 재생할 프레임 수")]
    public float frameRate = 12f;

    [Tooltip("SpriteRenderer의 sortingOrder")]
    public int sortingOrder;

    [Tooltip("재생 시 적용할 표시 스케일")]
    public Vector3 displayScale = Vector3.one;

}
