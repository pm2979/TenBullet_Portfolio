using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 발사 조건 판정부터 투사체 생성·비용 확정·성공 통지까지 실행
/// </summary>
[RequireComponent(typeof(ActiveWeapon))]
[RequireComponent(typeof(Health))]
[RequireComponent(typeof(WeaponFireEffects))]
[DisallowMultipleComponent]
public class FireWeapon : MonoBehaviour
{
    private readonly FireDecision fireDecision = new FireDecision();
    private readonly ProjectileSpawner projectileSpawner = new ProjectileSpawner();
    private readonly ProjectileEmissionPlanner emissionPlanner = new ProjectileEmissionPlanner();
    private FireCostType fireCostType = FireCostType.None;
    // 0을 식별자 없음으로 유지하기 위한 다음 발사 번호
    private int nextShotId = 1;
    private ActiveWeapon activeWeapon;
    private Health health;
    private WeaponFireEffects weaponFireEffects;
    private Stats stats;

    // 플레이어에게만 적용하는 이동 반동과 발사 후 감속 대상
    private PlayerMotor2D playerMotor2D;
    private PlayerController playerController;

    /// <summary>
    /// 하나 이상의 투사체가 생성된 발사 시퀀스 종료 또는 무투사체 발사 성공 통지
    /// </summary>
    public event Action<Weapon> Fired;

    private void Awake()
    {
        activeWeapon = GetComponent<ActiveWeapon>();
        health = GetComponent<Health>();
        weaponFireEffects = GetComponent<WeaponFireEffects>();
        stats = GetComponent<Stats>();
        playerMotor2D = GetComponent<PlayerMotor2D>();
        playerController = GetComponent<PlayerController>();
    }

    // 플레이어 전용 컴포넌트에 발사 반동과 이동 감속 요청
    private void ApplyPlayerFireMovementEffects(Weapon weapon, float aimAngle, float finalFiringMoveSpeedMultiplier)
    {
        playerController?.StartFiringSlowdown(finalFiringMoveSpeedMultiplier, weapon.weaponDetails.weaponMoveSlowdownDuration);

        if (playerMotor2D != null)
        {
            Vector2 recoilDirection = -DirectionUtility2D.DirectionFromDegrees(aimAngle);
            playerMotor2D.TryStartMovementRecoil(recoilDirection, weapon.weaponDetails.weaponMovementRecoilSpeed, weapon.weaponDetails.weaponMovementRecoilDuration);
        }
    }

    // 비활성화 시 진행 중인 시퀀스와 입력 세션을 정리하되 이미 생성된 투사체는 유지
    private void OnDisable()
    {
        StopAllCoroutines();
        fireDecision.ResetSession();
    }

    private void Update()
    {
        fireDecision.TickCoolDown(Time.deltaTime);
    }

    /// <summary>
    /// 소유자 유형에 의존하지 않고 발사 시 소비할 자원 종류 설정
    /// </summary>
    public void SetFireCostType(FireCostType fireCostType)
    {
        this.fireCostType = fireCostType;
    }

    // 발사 행위 단위의 양수 식별자 발급
    private int IssueShotId()
    {
        int shotId = nextShotId;
        nextShotId++;
        return shotId;
    }

    /// <summary>
    /// 입력 상태와 조준 결과를 사용해 발사 처리 요청
    /// </summary>
    public void RequestFire(FireWeaponRequest request)
    {
        ProcessFireRequest(request);
    }

    /// <summary>
    /// 투사체 없이 쿨다운·비용·성공 효과를 확정하고 발사 식별자 반환
    /// </summary>
    public bool TryFireWithoutProjectile(WeaponAimData aimData, out int shotId)
    {
        Weapon weapon = activeWeapon.GetCurrentWeapon();

        if (weapon == null)
        {
            shotId = 0;
            return false;
        }

        FireReadiness readiness = fireDecision.EvaluateImmediate(fireCostType, weapon, health.GetCurrentHealth());

        if (readiness != FireReadiness.Ready)
        {
            shotId = 0;
            return false;
        }

        fireDecision.BeginReservation();
        float finalFireInterval = CurrentFireInterval(weapon);
        fireDecision.ResetCoolDown(finalFireInterval);

        if (fireCostType == FireCostType.Health && health.GetCurrentHealth() >= weapon.weaponDetails.healthCostPerFire)
        {
            health.ConsumeHealth(weapon.weaponDetails.healthCostPerFire);
        }

        float finalFiringMoveSpeedMultiplier = ProjectileFireSnapshotResolver.ResolveFiringMoveSpeedMultiplier(health, weapon.weaponDetails.weaponFiringMoveSpeedMultiplier);

        weaponFireEffects.TryPlayMuzzleSpriteEffect(weapon);
        weaponFireEffects.TrySpawnBulletShell(weapon, aimData.AimAngle);
        weaponFireEffects.PlaySound(weapon, finalFireInterval);
        weaponFireEffects.PlayVisualRecoil(weapon);
        ApplyPlayerFireMovementEffects(weapon, aimData.AimAngle, finalFiringMoveSpeedMultiplier);

        Fired?.Invoke(weapon);

        shotId = IssueShotId();
        return true;
    }

    // 요청 시작 시점의 무기를 고정한 뒤 선충전과 발사 조건 평가
    private void ProcessFireRequest(FireWeaponRequest request)
    {
        // 시퀀스 도중 장착 상태가 바뀌어도 발사 출처를 보존하기 위한 스냅샷
        Weapon weapon = activeWeapon.GetCurrentWeapon();

        UpdatePreCharge(weapon, request);

        if (request.IsFiring)
        {
            FireReadiness readiness = fireDecision.Evaluate(fireCostType, weapon, health.GetCurrentHealth());

            if (readiness == FireReadiness.Ready && weapon.weaponDetails.projectileDefinition != null)
            {
                // 첫 생성 결과가 확정되기 전까지 중복 승인 방지
                fireDecision.BeginReservation();

                StartProjectileSequence(weapon, request.WeaponAimData.AimAngle, request.WeaponAimData.WeaponAimAngle);
            }
        }
    }

    // RuntimeStats가 있으면 보정하고 없으면 무기 기본 발사 간격 반환
    private float CurrentFireInterval(Weapon weapon)
    {
        if (stats != null && stats.RuntimeStats != null)
        {
            return stats.RuntimeStats.ApplyModifiers(StatType.FireInterval, weapon.weaponDetails.weaponFireRate);
        }

        return weapon.weaponDetails.weaponFireRate;
    }

    // 현재 입력 연속 여부에 따라 선충전 상태 갱신
    private void UpdatePreCharge(Weapon weapon, FireWeaponRequest request)
    {
        fireDecision.UpdatePreCharge(request.FirePreviousFrame, Time.deltaTime, weapon.weaponDetails.weaponPrechargeTime);
    }

    // 유효한 투사체 정의로 발사 시퀀스 시작
    private void StartProjectileSequence(Weapon weapon, float aimAngle, float weaponAimAngle)
    {
        ProjectileDataSO projectileDefinition = weapon.weaponDetails.projectileDefinition;

        if (projectileDefinition != null)
        {
            StartCoroutine(RunProjectileSequence(weapon, projectileDefinition, aimAngle, weaponAimAngle));
        }
    }

    // 계획된 투사체를 순서대로 생성하고 첫 성공 시 비용과 일회성 효과 확정
    private IEnumerator RunProjectileSequence(Weapon weapon, ProjectileDataSO projectileDefinition, float aimAngle, float weaponAimAngle)
    {
        // 근거리에서도 클릭 지점을 비껴가지 않도록 발사구 기준 각도 유지
        float resolvedAimAngle = weaponAimAngle;

        // 멀티샷 전체를 하나의 발사 행위로 묶기 위한 공통 식별자
        int shotId = IssueShotId();

        ProjectileEmissionSettings emissionSettings = ProjectileEmissionSettings.FromWeapon(weapon.weaponDetails, projectileDefinition);
        IReadOnlyList<ProjectileEmissionStep> steps = emissionPlanner.Plan(emissionSettings);

        // 시간차 발사 중 능력치 변경이 소급되지 않도록 승인 시점 값 고정
        int finalDamage = weapon.weaponDetails.weaponDamage;
        float finalRange = ProjectileFireSnapshotResolver.ResolveRange(health, weapon.weaponDetails.weaponRange);
        int finalPierceCount = ProjectileFireSnapshotResolver.ResolvePierceCount(health, projectileDefinition.projectilePierceCount);
        float finalSpeed = ProjectileFireSnapshotResolver.ResolveProjectileSpeed(health, weapon.weaponDetails.weaponProjectileSpeed);
        float finalFiringMoveSpeedMultiplier = ProjectileFireSnapshotResolver.ResolveFiringMoveSpeedMultiplier(health, weapon.weaponDetails.weaponFiringMoveSpeedMultiplier);
        CombatDamageMath.DamageSnapshot damageSnapshot = CombatDamageMath.CreateSnapshot(health);

        bool anySucceeded = false;
        bool hasPlayedFirstSuccessEffect = false;

        for (int i = 0; i < steps.Count; i++)
        {
            ProjectileEmissionStep step = steps[i];

            // 소유자 계층과 무기 콜라이더를 제외하고 공격자·진영 정보를 함께 전달
            ProjectileLaunchRequest request = new ProjectileLaunchRequest(
                step.Definition,
                activeWeapon.GetShootPosition(),
                resolvedAimAngle + step.AngleOffset,
                finalSpeed,
                finalDamage,
                finalRange,
                finalPierceCount,
                damageSnapshot,
                activeWeapon.GetWeaponCollider(),
                attacker: health,
                sourceFaction: health.Faction,
                weapon: weapon,
                knockbackSpeed: weapon.weaponDetails.weaponKnockbackSpeed,
                knockbackDuration: weapon.weaponDetails.weaponKnockbackDuration,
                shotId: shotId);

            ProjectileSpawnResult spawnResult = projectileSpawner.TrySpawn(step.Prefab, request);

            if (!spawnResult.Succeeded)
            {
                Logger.LogWarning($"FireWeapon: 투사체 프리팹 '{step.Prefab.name}' 발사 실패({spawnResult.Failure})", this);
            }
            else
            {
                anySucceeded = true;

                if (!hasPlayedFirstSuccessEffect)
                {
                    hasPlayedFirstSuccessEffect = true;

                    // 실제 발사 실패에 쿨다운을 부과하지 않도록 첫 생성 성공 시 확정
                    float finalFireInterval = CurrentFireInterval(weapon);
                    fireDecision.ResetCoolDown(finalFireInterval);

                    // 승인 후 자원 변동을 반영해 첫 생성 성공 시 비용을 한 번만 재확인
                    if (fireCostType == FireCostType.Health && health.GetCurrentHealth() >= weapon.weaponDetails.healthCostPerFire)
                    {
                        health.ConsumeHealth(weapon.weaponDetails.healthCostPerFire);
                    }

                    // 멀티샷에서도 발사 성공 효과는 첫 생성 성공 시 한 번만 적용
                    weaponFireEffects.TryPlayMuzzleSpriteEffect(weapon);
                    weaponFireEffects.TrySpawnBulletShell(weapon, aimAngle);
                    weaponFireEffects.PlaySound(weapon, finalFireInterval);
                    weaponFireEffects.PlayVisualRecoil(weapon);
                    ApplyPlayerFireMovementEffects(weapon, aimAngle, finalFiringMoveSpeedMultiplier);
                }
            }

            yield return new WaitForSeconds(step.DelayAfter);
        }

        if (!anySucceeded)
        {
            // 전체 생성 실패에는 비용과 쿨다운을 적용하지 않고 즉시 재시도 허용
            fireDecision.CancelReservation();
            yield break;
        }

        // 시간차 멀티샷까지 완료된 성공 시퀀스를 한 번만 통지
        Fired?.Invoke(weapon);
    }
}
