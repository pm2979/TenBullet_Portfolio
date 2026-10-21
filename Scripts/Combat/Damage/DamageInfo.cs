using UnityEngine;

/// <summary>
/// 피해 판정에 필요한 공격 주체와 요청 피해량을 전달하는 불변 값 타입이다.
/// </summary>
public readonly struct DamageInfo
{
    public readonly Faction SourceFaction;
    public readonly Component Attacker;
    public readonly int Amount;

    /// <summary>이 공격을 패링으로 무효화할 수 있는지. 공격 Producer가 명시하며 기본값은 패링 불가다</summary>
    public readonly bool IsParryable;

    /// <summary>공격이 피격 대상 쪽으로 진행한 월드 방향. 성공 연출 배치에만 사용하며 판정 성공 여부에는 영향을 주지 않는다</summary>
    public readonly Vector2 IncomingDirection;

    // 패링 가능 여부를 명시하지 않은 기존 공격은 패링되지 않는 피해로 구성한다.
    public DamageInfo(Faction sourceFaction, Component attacker, int amount)
        : this(sourceFaction, attacker, amount, false, Vector2.zero)
    {
    }

    // 피해 구현체가 진영 정책·패링 판정과 실제 피해량을 판정할 수 있도록 요청 정보를 구성한다.
    public DamageInfo(Faction sourceFaction, Component attacker, int amount, bool isParryable, Vector2 incomingDirection)
    {
        SourceFaction = sourceFaction;
        Attacker = attacker;
        Amount = amount;
        IsParryable = isParryable;
        IncomingDirection = incomingDirection;
    }
}
