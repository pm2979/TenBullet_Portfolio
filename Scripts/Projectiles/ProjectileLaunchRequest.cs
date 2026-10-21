using UnityEngine;

/// <summary>
/// 투사체 한 발의 생성·이동·전투에 사용할 발사 시점의 불변 값입니다.
/// </summary>
public readonly struct ProjectileLaunchRequest
{
    /// <summary>
    /// 호출자가 능력치 보정까지 마친 값을 전달합니다. 투사체는 비행 중 능력치를 다시 계산하지 않습니다.
    /// </summary>
    public ProjectileLaunchRequest(
        ProjectileDataSO definition,
        Vector3 spawnPosition,
        float aimAngle,
        float speed,
        int damage,
        float range,
        int pierceCount,
        CombatDamageMath.DamageSnapshot damageSnapshot,
        Collider2D sourceCollider = null,
        bool overrideMovement = false,
        Component attacker = null,
        Faction sourceFaction = Faction.Neutral,
        Weapon weapon = null,
        float knockbackSpeed = 0f,
        float knockbackDuration = 0f,
        int shotId = 0)
    {
        Definition = definition;
        SpawnPosition = spawnPosition;
        AimAngle = aimAngle;
        Speed = speed;
        Damage = damage;
        Range = range;
        PierceCount = pierceCount;
        DamageSnapshot = damageSnapshot;
        SourceCollider = sourceCollider;
        OverrideMovement = overrideMovement;
        Attacker = attacker;
        SourceFaction = sourceFaction;
        Weapon = weapon;
        KnockbackSpeed = knockbackSpeed;
        KnockbackDuration = knockbackDuration;
        ShotId = shotId;
    }

    public ProjectileDataSO Definition { get; }
    public Vector3 SpawnPosition { get; }
    public float AimAngle { get; }
    public float Speed { get; }

    /// <summary>발사 시점에 확정한 최종 기본 피해량입니다.</summary>
    public int Damage { get; }

    /// <summary>누적 이동 거리로 소비할 최종 사거리입니다. 0 이하는 유효하지 않습니다.</summary>
    public float Range { get; }

    /// <summary>첫 적대 대상 이후 추가로 관통할 수 있는 고유 대상 수입니다.</summary>
    public int PierceCount { get; }

    /// <summary>명중마다 피해와 치명타를 계산할 때 사용하는 발사 시점의 배율 묶음입니다.</summary>
    public CombatDamageMath.DamageSnapshot DamageSnapshot { get; }

    public Collider2D SourceCollider { get; }
    public bool OverrideMovement { get; }

    /// <summary>
    /// 피해 출처이자 발사 직후 충돌을 무시할 콜라이더 계층의 기준입니다.
    /// </summary>
    public Component Attacker { get; }

    /// <summary>
    /// 피해 출처의 진영입니다. 같은 진영 대상은 적중으로 소비하지 않습니다.
    /// </summary>
    public Faction SourceFaction { get; }

    /// <summary>
    /// 적중 이벤트를 받을 무기입니다. 무기를 거치지 않은 공격이면 null입니다.
    /// </summary>
    public Weapon Weapon { get; }

    /// <summary>피해가 적용된 대상에 전달할 넉백 속도입니다.</summary>
    public float KnockbackSpeed { get; }

    /// <summary>피해가 적용된 대상에 전달할 넉백 지속 시간입니다.</summary>
    public float KnockbackDuration { get; }

    /// <summary>동일한 발사에서 생성된 투사체를 묶는 식별자입니다. 0은 식별자 없음을 뜻합니다.</summary>
    public int ShotId { get; }

    /// <summary>
    /// 전투 스냅샷은 유지하고 이동 소유권과 발사 주체 콜라이더만 바꾼 요청을 생성합니다.
    /// 콜라이더를 생략하면 기존 값을 유지합니다.
    /// </summary>
    public ProjectileLaunchRequest WithOverrideMovement(bool overrideMovement, Collider2D sourceCollider = null)
    {
        return new ProjectileLaunchRequest(
            Definition,
            SpawnPosition,
            AimAngle,
            Speed,
            Damage,
            Range,
            PierceCount,
            DamageSnapshot,
            sourceCollider != null ? sourceCollider : SourceCollider,
            overrideMovement,
            Attacker,
            SourceFaction,
            Weapon,
            KnockbackSpeed,
            KnockbackDuration,
            ShotId);
    }
}
