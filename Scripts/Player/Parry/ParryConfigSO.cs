using UnityEngine;

/// <summary>
/// 패링 판정 시간과 스태미나 비용의 기본값을 제공한다.
/// </summary>
[CreateAssetMenu(fileName = "ParryConfig_", menuName = "Scriptable Objects/Player/Parry Config")]
public class ParryConfigSO : ScriptableObject
{
    [Header("패링 정보")]
    [Tooltip("패링 판정을 유지하는 시간(초). ParryWindow Stat의 base이며 0보다 커야 시작할 수 있다")]
    [Min(0.01f)]
    [SerializeField] private float _parryWindow = 0.3f;

    [Tooltip("패링 1회 시작에 소비하는 Stamina. ParryStaminaCost Stat의 base이며 성공·만료·취소와 무관하게 환불하지 않는다")]
    [Min(0f)]
    [SerializeField] private float _staminaCost = 15f;

    public float ParryWindow => _parryWindow;
    public float StaminaCost => _staminaCost;
}
