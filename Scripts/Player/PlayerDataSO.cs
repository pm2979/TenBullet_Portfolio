using UnityEngine;

/// <summary>
/// 플레이어의 기본 능력치와 시작 장비를 정의한다.
/// </summary>
[CreateAssetMenu(fileName = "PlayerData_", menuName = "Scriptable Objects/Player/PlayerData")]
public class PlayerDataSO : ScriptableObject
{
    [Space(5)]
    [Header("플레이어 기본 정보")]
    [Tooltip("플레이어 이름")]
    public string playerName;

    [Tooltip("플레이어 프리팹 게임오브젝트")]
    public GameObject playerPrefab;

    [Space(5)]
    [Header("체력")]
    [Tooltip("플레이어의 시작 체력")]
    public int playerBaseHealth;
    [Tooltip("피격 직후 무적 시간이 있다면 선택. 선택 시 다른 필드에 무적 시간(초)을 지정")]
    public bool isInvulnerable = false;
    [Tooltip("피격 후 무적 시간(초)")]
    public float invulnerabilityDuration;

    [Space(5)]
    [Header("무기")]
    [Tooltip("플레이어의 초기 시작 무기")]
    public WeaponDataSO startingWeapon;

    [Space(5)]
    [Header("Stamina")]
    [Tooltip("RuntimeStats Modifier 집계를 적용할 Stamina 최종 최대치 base 값(StatType.MaxStamina). 0보다 커야 한다")]
    public float maxStamina = 100f;
    [Tooltip("RuntimeStats Modifier 집계를 적용할 초당 자동 회복량 base 값(StatType.StaminaRecoveryRate). 0 이상이어야 한다")]
    public float staminaRecoveryRate = 20f;
    [Tooltip("소비 성공 직후 자동 회복이 시작되기까지 대기하는 시간(초). Stat 연동 없이 이 값을 그대로 사용한다")]
    public float staminaRecoveryDelay = 1.5f;

    [Space(5)]
    [Header("시야")]
    [Tooltip("무기 사거리와 비교할 기본 시야 거리(월드 단위). 실제 시야는 max(이 값, 무기 최종 사거리)다")]
    public float baseViewDistance = 8f;
    [Tooltip("약점 노출 게이트의 base 값(StatType.WeakPointReveal). 0이면 약점이 보이지 않고, Implant·아이템이 Modifier로 켠다")]
    public float weakPointRevealBase = 0f;

}
