/// <summary>
/// 런타임 보정 규칙으로 계산할 능력치의 종류를 식별한다.
/// 각 값은 실제 소비 시스템과 계산 정책이 함께 존재할 때만 추가한다.
/// </summary>
public enum StatType
{
    /// <summary>플레이어와 적의 최대 체력</summary>
    MaxHealth,
    /// <summary>플레이어와 적의 이동 속도</summary>
    MoveSpeed,
    /// <summary>무기가 다음 발사를 허용하기까지의 간격</summary>
    FireInterval,
    /// <summary>발사 시점에 고정하는 투사체 이동 속도</summary>
    ProjectileSpeed,
    /// <summary>공격력 보정 후 적용하는 최종 피해 배율</summary>
    DamageMultiplier,
    /// <summary>각 적중이 치명타가 될 확률</summary>
    CriticalChance,
    /// <summary>치명타 적중에 적용하는 피해 배율</summary>
    CriticalMultiplier,
    /// <summary>무기와 투사체의 유효 사거리</summary>
    Range,
    /// <summary>첫 대상 적중 후 추가로 관통할 수 있는 대상 수</summary>
    PierceCount,
    /// <summary>발사 후 일반 이동 속도에 적용하는 배율</summary>
    FiringMoveSpeedMultiplier,
    /// <summary>플레이어가 약점을 직접 공격할 때 적용하는 피해 배율</summary>
    WeakPointDamageMultiplier,
    /// <summary>플레이어의 약점 탐지 활성 여부를 결정하는 값</summary>
    WeakPointReveal,
    /// <summary>콘텐츠가 제공한 기본 피해에 결합하는 공격력</summary>
    Attack,
    /// <summary>넉백 속도에 곱하는 저항 배율로, 0은 완전 저항이고 1은 저항 없음</summary>
    KnockbackResistance,
    /// <summary>플레이어와 적이 전투를 시작할 때 보유하는 보호막 중첩 수</summary>
    ShieldStackCount,
    /// <summary>플레이어 스태미나의 최대 보유량</summary>
    MaxStamina,
    /// <summary>플레이어 스태미나의 초당 자동 회복량</summary>
    StaminaRecoveryRate,
    /// <summary>구르기 시작 시 고정하는 이동 거리</summary>
    DodgeDistance,
    /// <summary>구르기 한 번을 시작할 때 소비하는 스태미나</summary>
    DodgeStaminaCost,
    /// <summary>구르기 시작 시 고정하는 무적 지속 시간</summary>
    DodgeInvulnerabilityDuration,
    /// <summary>패링 시작 시 고정하는 판정 유지 시간</summary>
    ParryWindow,
    /// <summary>패링 한 번을 시작할 때 소비하는 스태미나</summary>
    ParryStaminaCost
}
