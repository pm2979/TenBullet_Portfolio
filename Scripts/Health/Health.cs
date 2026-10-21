using System;
using UnityEngine;

/// <summary>
/// 플레이어·적의 체력을 관리하는 클래스
/// </summary>
[RequireComponent(typeof(InvulnerabilityController))]
[DisallowMultipleComponent]
public class Health : MonoBehaviour, IDamageable, IHealable
{
    [Tooltip("이 체력 보유자가 속한 진영. 자기 자신·아군 피해 필터에 사용하며 Player/Enemy 초기화 진입점에서 SetFaction으로 설정한다.")]
    [SerializeField] private Faction _faction = Faction.Neutral;

    private readonly HealthState healthState = new HealthState();
    private InvulnerabilityController invulnerabilityController;

    // 같은 GameObject의 선택적 Parry. Player에만 연결되며 없으면 null로 두고 기존 Shield/HP 순서를 그대로 따른다
    private PlayerParry playerParry;

    // 현재 피해를 허용할지 결정하는 외부 정책 (예: 플레이어 구르기 무적). 기본은 항상 허용
    private Func<bool> damageAllowedPolicy = () => true;

    /// <summary>
    /// 체력이 변경될 때마다 이전/현재 값과 원인을 담아 발생하는 이벤트.
    /// HealthChange는 struct이므로 발행 시 힙 할당이 없다
    /// </summary>
    public event Action<HealthChange> Changed;

    /// <summary>
    /// 이 체력 보유자가 속한 진영
    /// </summary>
    public Faction Faction => _faction;

    private void Awake()
    {
        invulnerabilityController = GetComponent<InvulnerabilityController>();
        playerParry = GetComponent<PlayerParry>();
    }

    /// <summary>
    /// 진영을 설정한다. Player/Enemy 등 소유자의 초기화 진입점에서 정확히 한 번 호출한다
    /// </summary>
    public void SetFaction(Faction faction)
    {
        _faction = faction;
    }

    /// <summary>
    /// 피격 후 무적 시간을 구성한다.
    /// Player/Enemy가 각자의 상세 데이터(PlayerDetailsSO/EnemyDetailsSO)로 초기화 시점에 호출한다
    /// </summary>
    public void ConfigureInvulnerability(bool isInvulnerabilityEnabled, float invulnerabilityDuration)
    {
        invulnerabilityController.Configure(isInvulnerabilityEnabled, invulnerabilityDuration);
    }

    /// <summary>
    /// 현재 피해를 허용할지 결정하는 외부 정책을 설정한다(예: 플레이어의 구르기 무적).
    /// 지정하지 않으면 항상 피해를 허용한다
    /// </summary>
    public void SetDamageAllowedPolicy(Func<bool> policy)
    {
        damageAllowedPolicy = policy ?? (() => true);
    }

    /// <summary>
    /// 피해를 입었을 때 호출되는 공개 메서드
    /// </summary>
    public void TakeDamage(int damageAmount)
    {
        if (damageAllowedPolicy() == false)
            return;

        if (invulnerabilityController.IsInvulnerable)
            return;

        ApplyDamage(damageAmount);
    }

    /// <summary>
    /// 진영·공격자 정보를 포함한 피해 적용을 시도한다.
    /// 피해 허용 정책, 무적, 같은 진영, Parry, Shield Stack 순으로 거부될 수 있으며 거부 시 Changed를 발행하지 않는다.
    /// Parry가 성공하면 Shield Stack을 보존한 채 Parried를 반환하고, Shield가 차단하면 Stack 1개를 소비하고
    /// HP·피격 무적 없이 ShieldBlocked를 반환한다.
    /// 적용되면 실제 반영된 피해량과, 이번 적용이 살아있는 상태에서 사망 상태로 전환시켰는지를 결과에 담는다
    /// </summary>
    public DamageResult TryTakeDamage(DamageInfo damageInfo)
    {
        if (damageAllowedPolicy() == false)
            return DamageResult.Rejected(DamageRejectReason.PolicyBlocked);

        if (invulnerabilityController.IsInvulnerable)
            return DamageResult.Rejected(DamageRejectReason.Invulnerable);

        if (damageInfo.SourceFaction != Faction.Neutral && damageInfo.SourceFaction == _faction)
            return DamageResult.Rejected(DamageRejectReason.SameFaction);

        // 이미 거부될 요청이 패링 기회를 소비하지 않도록 위 거부 조건을 모두 통과한 뒤에 판정한다.
        // Shield보다 먼저 처리해 패링 성공이 Stack을 소비하지 않게 한다
        if (playerParry != null && playerParry.TryParry(damageInfo))
            return DamageResult.Rejected(DamageRejectReason.Parried);

        Shield shield = GetComponent<Shield>();
        if (shield != null && shield.IsActive)
        {
            shield.ConsumeStack();
            return DamageResult.Rejected(DamageRejectReason.ShieldBlocked);
        }

        int previousHealth = healthState.CurrentHealth;
        bool wasTargetAlreadyDead = previousHealth <= 0;

        ApplyDamage(damageInfo.Amount);

        int currentHealth = healthState.CurrentHealth;
        bool wasKilled = previousHealth > 0 && currentHealth <= 0;

        return DamageResult.Applied(previousHealth - currentHealth, wasKilled, wasTargetAlreadyDead);
    }

    /// <summary>
    /// 체력에 피해를 반영하고 변경을 발행한 뒤 피격 무적을 활성화한다
    /// </summary>
    private void ApplyDamage(int damageAmount)
    {
        int previousHealth = healthState.CurrentHealth;

        healthState.ApplyDamage(damageAmount);
        RaiseChanged(HealthChangeReason.Damage, previousHealth);

        invulnerabilityController.ActivateInvulnerability();
    }

    /// <summary>
    /// 체력 변경 내역을 Changed 구독자에게 전달한다
    /// </summary>
    private void RaiseChanged(HealthChangeReason reason, int previousHealth)
    {
        int currentHealth = healthState.CurrentHealth;

        Changed?.Invoke(new HealthChange(previousHealth, currentHealth, healthState.MaxHealth, currentHealth - previousHealth, reason));
    }

    /// <summary>
    /// 시작 체력을 설정한다
    /// </summary>
    public void SetStartingHealth(int startingHealth)
    {
        int previousHealth = healthState.CurrentHealth;

        if (healthState.SetStartingHealth(startingHealth))
        {
            RaiseChanged(HealthChangeReason.Initialize, previousHealth);
        }
    }

    /// <summary>
    /// 시작 체력을 반환한다
    /// </summary>
    public int GetStartingHealth()
    {
        return healthState.StartingHealth;
    }

    /// <summary>
    /// 최대 체력을 설정한다. StartingHealth와 달리 런타임 중 반복 호출될 수 있으며(예: Stats의
    /// Final MaxHealth 변경), 증가 시 증가량만큼 CurrentHealth를 회복하고 감소 시 CurrentHealth가
    /// 새 최대값을 넘지 않도록 Clamp한다(system-design.md Decision 010)
    /// </summary>
    public void SetMaxHealth(int maxHealth)
    {
        int previousHealth = healthState.CurrentHealth;

        if (healthState.SetMaxHealth(maxHealth))
        {
            RaiseChanged(HealthChangeReason.MaxHealthChanged, previousHealth);
        }
    }

    /// <summary>
    /// 최대 체력을 반환한다
    /// </summary>
    public int GetMaxHealth()
    {
        return healthState.MaxHealth;
    }

    /// <summary>
    /// 현재 체력을 반환한다
    /// </summary>
    public int GetCurrentHealth()
    {
        return healthState.CurrentHealth;
    }

    /// <summary>
    /// 피격 무적/구르기 판정 없이 체력을 소모한다 (예: 총알로 사용되는 체력 소모)
    /// </summary>
    public void ConsumeHealth(int amount)
    {
        int previousHealth = healthState.CurrentHealth;

        healthState.ConsumeHealth(amount);
        RaiseChanged(HealthChangeReason.ResourceCost, previousHealth);
    }

    /// <summary>
    /// 지정된 비율만큼 체력을 회복한다
    /// </summary>
    public void AddHealth(int healthPercent)
    {
        int previousHealth = healthState.CurrentHealth;

        healthState.AddHealth(healthPercent);
        RaiseChanged(HealthChangeReason.Heal, previousHealth);
    }

    /// <summary>
    /// 지정된 절대값만큼 체력 회복을 시도한다. 백분율 기반인 AddHealth와 의미를 섞지 않는 별도 진입점이며,
    /// 실제로 적용된 증가량이 없으면 Changed를 발행하지 않는다
    /// </summary>
    public HealResult TryRestoreHealth(HealInfo healInfo)
    {
        int previousHealth = healthState.CurrentHealth;
        int appliedAmount = healthState.RestoreHealth(healInfo.Amount);

        if (appliedAmount == 0)
            return HealResult.NotApplied;

        RaiseChanged(HealthChangeReason.Heal, previousHealth);

        return HealResult.Applied(appliedAmount);
    }
}
