using System;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Health))]
[RequireComponent(typeof(InvulnerabilityController))]
[RequireComponent(typeof(HitFeedback))]
[RequireComponent(typeof(PlayerController))]
[RequireComponent(typeof(FireWeapon))]
[RequireComponent(typeof(ActiveWeapon))]
[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(Rigidbody2D))]
[DisallowMultipleComponent]

/// <summary>
/// 플레이어 구성 요소를 연결하고 런타임 능력치와 시작 장비를 초기화한다.
/// </summary>
[DefaultExecutionOrder(-100)]
public class Player : MonoBehaviour
{
    [HideInInspector] public PlayerDataSO playerDetails;
    [HideInInspector] public Health health;
    [HideInInspector] public InvulnerabilityController hitImmunity;
    [HideInInspector] public HitFeedback hitFeedback;
    [HideInInspector] public PlayerController playerController;
    [HideInInspector] public ActiveWeapon activeWeapon;
    [HideInInspector] public FireWeapon fireWeapon;
    [HideInInspector] public Stats stats;
    [HideInInspector] public Shield shield;
    [HideInInspector] public KnockbackReceiver knockbackReceiver;
    [HideInInspector] public WeaponImplantLoadout weaponImplantLoadout;
    [HideInInspector] public Stamina stamina;

    // Visual 자식의 표현 컴포넌트는 루트에서 조회할 수 없어 Inspector 참조로 연결한다.
    [Header("Visual (자식 GameObject)")]
    [SerializeField] private AimWeapon _aimWeapon;
    [SerializeField] private SpriteRenderer _spriteRenderer;
    [SerializeField] private Animator _animator;

    public AimWeapon aimWeapon => _aimWeapon;
    public SpriteRenderer spriteRenderer => _spriteRenderer;
    public Animator animator => _animator;

    /// <summary>
    /// 시작 능력치와 장비 초기화가 끝났는지 나타낸다.
    /// </summary>
    public bool IsInitialized { get; private set; }

    /// <summary>
    /// 사망 유예 종료 후에도 체력이 0이면 비활성화 직전에 발생한다.
    /// </summary>
    public event Action Died;

    private Coroutine _delayedDeathCoroutine;

    private void Awake()
    {
        health = GetComponent<Health>();
        hitImmunity = GetComponent<InvulnerabilityController>();
        hitFeedback = GetComponent<HitFeedback>();
        playerController = GetComponent<PlayerController>();
        activeWeapon = GetComponent<ActiveWeapon>();
        fireWeapon = GetComponent<FireWeapon>();
        stats = GetComponent<Stats>();
        shield = GetComponent<Shield>();
        knockbackReceiver = GetComponent<KnockbackReceiver>();
        weaponImplantLoadout = GetComponent<WeaponImplantLoadout>();

        // 플레이어 진영을 명시해 자기 자신과 아군 피해를 필터링한다.
        health.SetFaction(Faction.Player);

        // 회피 무적과 피격 무적의 수명을 분리해 한쪽 종료가 다른 무적을 해제하지 않게 한다.
        health.SetDamageAllowedPolicy(null);

        // 발사 비용 정책을 명시적으로 주입해 컴포넌트 유무로 플레이어를 추론하지 않게 한다.
        fireWeapon.SetFireCostType(FireCostType.Health);
    }

    /// <summary>
    /// 플레이어 데이터로 능력치와 시작 장비를 한 번 초기화한다.
    /// 중복 호출은 기존 런타임 상태를 보존하고 무시한다.
    /// </summary>
    public void Initialize(PlayerDataSO playerDetails)
    {
        if (IsInitialized)
        {
            Logger.LogWarning($"{nameof(Player)}가 이미 초기화된 상태에서 {nameof(Initialize)}가 다시 호출되어 무시합니다.", this);
            return;
        }

        this.playerDetails = playerDetails;

        // 시작 무기는 초기화 단계에서 생성해 활성 무기 확정 시점을 하나로 유지한다.
        Weapon startingWeapon = new Weapon
        {
            weaponDetails = playerDetails.startingWeapon
        };
        activeWeapon.SetWeapon(startingWeapon);

        // 런타임 무기와 체력은 탐색할 수 없으므로 초기화 시 장착 구성에 직접 주입한다.
        weaponImplantLoadout?.Equip(startingWeapon, health);

        // 파생 수치를 사용하는 자원보다 먼저 런타임 능력치를 초기화한다.
        stats?.Initialize();

        SetPlayerHealth();

        SetPlayerShield();

        SetPlayerKnockbackResistance();

        SetPlayerStamina();

        health.ConfigureInvulnerability(playerDetails.isInvulnerable, playerDetails.invulnerabilityDuration);

        hitFeedback.Configure(health, spriteRenderer);

        IsInitialized = true;
    }

    private void OnEnable()
    {
        health.Changed += Health_Changed;
    }

    private void OnDisable()
    {
        health.Changed -= Health_Changed;
    }

    // 체력이 소진되면 사망 유예를 시작하고, 회복되면 진행 중인 유예를 취소한다.
    private void Health_Changed(HealthChange healthChange)
    {
        if (healthChange.CurrentHealth <= 0)
        {
            if (_delayedDeathCoroutine == null)
                _delayedDeathCoroutine = StartCoroutine(DelayedDeathRoutine());
        }
        else if (_delayedDeathCoroutine != null)
        {
            StopCoroutine(_delayedDeathCoroutine);
            _delayedDeathCoroutine = null;
        }
    }

    // 유예 종료 시 체력이 여전히 소진 상태일 때만 사망을 알리고 플레이어를 비활성화한다.
    private IEnumerator DelayedDeathRoutine()
    {
        yield return new WaitForSeconds(5f);

        if (health.GetCurrentHealth() <= 0)
        {
            Died?.Invoke();
            gameObject.SetActive(false);
        }

        _delayedDeathCoroutine = null;
    }

    // 기본 체력에 런타임 보정값을 적용해 최대 체력과 현재 체력을 함께 초기화한다.
    private void SetPlayerHealth()
    {
        health.SetStartingHealth(playerDetails.playerBaseHealth);

        if (stats != null && stats.RuntimeStats != null)
        {
            health.SetMaxHealth(Mathf.RoundToInt(stats.RuntimeStats.GetFinalValue(StatType.MaxHealth)));
        }
    }

    // 선택 구성인 Shield가 있으면 데이터에 지정된 시작 스택을 적용한다.
    private void SetPlayerShield()
    {
        shield = GetComponent<Shield>();

        if (shield == null || stats == null || stats.RuntimeStats == null)
        {
            return;
        }

        shield.SetStackCount(stats.RuntimeStats.ShieldStackCount);
    }

    // 선택 구성인 넉백 수신기가 있으면 데이터의 저항 배율을 적용한다.
    private void SetPlayerKnockbackResistance()
    {
        knockbackReceiver = GetComponent<KnockbackReceiver>();

        if (knockbackReceiver == null || stats == null || stats.RuntimeStats == null)
        {
            return;
        }

        knockbackReceiver.SetResistance(stats.RuntimeStats.GetFinalValue(StatType.KnockbackResistance));
    }

    // 스태미나와 런타임 능력치가 모두 있으면 최종 최대치로 완전 회복 상태를 만든다.
    private void SetPlayerStamina()
    {
        stamina = GetComponent<Stamina>();

        if (stamina == null || stats == null || stats.RuntimeStats == null)
        {
            return;
        }

        stamina.Initialize(stats.RuntimeStats, playerDetails);
    }

    /// <summary>
    /// 현재 플레이어의 월드 위치를 반환한다.
    /// </summary>
    public Vector3 GetPlayerPosition()
    {
        return transform.position;
    }
}
