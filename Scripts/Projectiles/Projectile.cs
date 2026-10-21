using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
// 발사 스냅샷을 받아 이동 구간 충돌·관통·피해 전달·풀 반환까지 한 투사체의 수명을 관리합니다.
public class Projectile : MonoBehaviour, IProjectile
{
    // 접촉 뒤 같은 이동 구간을 계속 검사할지, 현재 위치에서 비행을 끝낼지 구분합니다.
    private enum ContactOutcome
    {
        Passed,
        Pierced,
        Stopped,
    }

    // 사실상 같은 위치의 벽과 대상을 함께 감지하면 벽 뒤 대상을 맞히지 않도록 벽을 우선합니다.
    private const float SameDistanceTolerance = 0.001f;
    // 외부 이동의 부동소수점 오차를 실제 위치 변경으로 오인하지 않는 허용치입니다.
    private const float UnverifiedMovementTolerance = 0.0001f;

    // 일반 Collider를 장애물로 오인하지 않도록 명시된 환경 레이어만 비행을 차단합니다.
    private const string WallLayerName = "Wall";

    [Tooltip("자식 오브젝트의 TrailRenderer 컴포넌트")]
    [SerializeField] private TrailRenderer _trailRenderer;

    private float projectileRange = 0f;
    private float projectileSpeed;
    private Vector3 fireDirectionVector;
    private float fireDirectionAngle;
    private SpriteRenderer spriteRenderer;
    private Collider2D projectileCollider;
    // 구간 검사 컬렉션은 풀 재대여와 매 프레임 검사에서 재사용해 반복 할당을 피합니다.
    private ContactFilter2D sweepContactFilter;
    private readonly List<RaycastHit2D> sweepHits = new List<RaycastHit2D>();
    private readonly List<Collider2D> ownColliders = new List<Collider2D>();
    private bool sweepEnabled;
    private int wallLayer;
    private ProjectileDataSO projectileDefinition;
    private float projectileChargeTimer;
    private bool overrideMovement;
    // 마지막으로 충돌 검사를 마친 위치를 다음 구간의 시작점으로 사용합니다.
    private Vector3 lastVerifiedPosition;
    private float accumulatedDistance;
    // 종료 결정과 대상 중복 기록을 분리해 관통 중에는 다음 접촉을 허용하고 중복 반환만 막습니다.
    private bool isColliding = false;
    private readonly List<Collider2D> ignoredColliders = new List<Collider2D>();
    private readonly HashSet<IDamageable> hitTargets = new HashSet<IDamageable>();
    private int remainingPierceCount;
    private int damage;
    private Component attacker;
    private Faction attackerFaction;
    private Weapon weapon;
    private CombatDamageMath.DamageSnapshot damageSnapshot;
    private float knockbackSpeed;
    private float knockbackDuration;
    private int shotId;

    /// <summary>가장 최근 적대 대상 접촉의 전투 결과입니다. 관찰용이며 비행 규칙에는 사용하지 않습니다.</summary>
    public CombatResult? LastCombatResult { get; private set; }

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        projectileCollider = GetComponent<Collider2D>();
        wallLayer = LayerMask.NameToLayer(WallLayerName);
    }

    private void OnDisable()
    {
        // 풀 재대여가 이전 발사의 출처·충돌 무시·적중·이동·궤적 상태를 이어받지 않도록 초기화합니다.
        ClearIgnoredColliders();
        hitTargets.Clear();
        remainingPierceCount = 0;
        attacker = null;
        attackerFaction = Faction.Neutral;
        weapon = null;
        damageSnapshot = default;
        shotId = 0;
        lastVerifiedPosition = Vector3.zero;
        accumulatedDistance = 0f;
        sweepHits.Clear();
        sweepEnabled = false;
        _trailRenderer.Clear();
        _trailRenderer.emitting = false;
    }

    private void Update()
    {
        if (projectileChargeTimer > 0f)
        {
            projectileChargeTimer -= Time.deltaTime;
            return;
        }

        Vector3 requestedEndPosition = transform.position;

        if (!overrideMovement)
        {
            requestedEndPosition += fireDirectionVector * projectileSpeed * Time.deltaTime;
        }

        MoveThroughSegment(requestedEndPosition);
    }

    // 기본 이동과 외부 이동을 같은 검증 구간으로 처리하고 남은 사거리 안의 위치만 확정합니다.
    private void MoveThroughSegment(Vector3 requestedEndPosition)
    {
        Vector3 segmentVector = requestedEndPosition - lastVerifiedPosition;
        float requestedLength = segmentVector.magnitude;
        float remainingRange = Mathf.Max(0f, projectileRange - accumulatedDistance);
        bool reachedRange = requestedLength >= remainingRange;
        float allowedLength = reachedRange ? remainingRange : requestedLength;

        if (requestedLength > 0f && allowedLength > 0f
            && SweepSegmentForContacts(segmentVector / requestedLength, allowedLength))
        {
            // 종료 접촉이 확정한 위치를 요청 끝점으로 덮어쓰지 않습니다.
            return;
        }

        Vector3 segmentEndPosition = requestedLength > 0f
            ? lastVerifiedPosition + segmentVector * (allowedLength / requestedLength)
            : lastVerifiedPosition;

        transform.position = segmentEndPosition;
        accumulatedDistance += allowedLength;
        lastVerifiedPosition = segmentEndPosition;

        if (reachedRange)
        {
            ReturnOrDisable();
        }
    }

    // 투사체 Collider 형상을 허용 거리만큼 투사하고 접촉을 실제 진행 순서대로 처리합니다.
    private bool SweepSegmentForContacts(Vector2 travelDirection, float allowedLength)
    {
        if (!sweepEnabled)
        {
            return false;
        }

        // Collider 자체를 투사해 Offset·Scale·회전을 별도 형상 계산 없이 그대로 반영합니다.
        int hitCount = projectileCollider.Cast(
            lastVerifiedPosition, transform.eulerAngles.z, travelDirection,
            sweepContactFilter, sweepHits, allowedLength, ignoreSiblingColliders: true);

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hit = sweepHits[ResolveBlockingFirstIndex(i, hitCount)];

            if (ProcessContact(hit.collider, hit.point, travelDirection) != ContactOutcome.Stopped)
            {
                // 적중 콜백이 투사체를 반환했다면 비활성 인스턴스로 남은 대상을 처리하지 않습니다.
                if (!isActiveAndEnabled)
                {
                    return true;
                }

                continue;
            }

            Vector3 stopPosition = lastVerifiedPosition + (Vector3)(travelDirection * hit.distance);
            transform.position = stopPosition;
            accumulatedDistance += hit.distance;
            lastVerifiedPosition = stopPosition;

            ReturnOrDisable();
            return true;
        }

        return false;
    }

    // 같은 위치로 간주되는 접촉 중 벽을 먼저 골라 벽 너머 피해를 막습니다.
    private int ResolveBlockingFirstIndex(int index, int hitCount)
    {
        float baseDistance = sweepHits[index].distance;

        for (int i = index; i < hitCount && sweepHits[i].distance - baseDistance <= SameDistanceTolerance; i++)
        {
            if (IsBlockingEnvironment(sweepHits[i].collider))
            {
                return i;
            }
        }

        return index;
    }

    // 스폰 겹침과 충전 중 접촉도 구간 검사와 동일한 판정 규칙으로 처리합니다.
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (isColliding) return;

        if (projectileDefinition == null)
        {
            // 발사 전이나 반환 후 도착한 물리 콜백에는 유효한 전투 문맥이 없습니다.
            return;
        }

        if (HasUnverifiedMovement())
        {
            // 외부 이동 구간을 먼저 검사해 사거리 밖 대상이 Trigger로 선적중되는 것을 막습니다.
            return;
        }

        Vector2 contactPosition = collision.ClosestPoint(transform.position);

        if (ProcessContact(collision, contactPosition, fireDirectionVector) == ContactOutcome.Stopped)
        {
            ReturnOrDisable();
        }
    }

    // 접촉 대상을 통과·관통·종료로 판정하며 실제 반환은 위치를 확정할 호출자에게 맡깁니다.
    private ContactOutcome ProcessContact(Collider2D other, Vector2 hitPosition, Vector2 travelDirection)
    {
        if (other == null || IsIgnoredCollider(other))
        {
            return ContactOutcome.Passed;
        }

        // Hurtbox만 피해 경계로 인정해 Body나 WeakPoint가 레이어 실수로 일반 피격되는 것을 막습니다.
        IDamageable damageable = GetValidDamageable(other);

        if (damageable != null)
        {
            // 아군과 이미 처리한 대상은 관통 횟수와 적중 연출을 소비하지 않습니다.
            if (IsSameFaction(damageable.Faction) || hitTargets.Contains(damageable))
            {
                return ContactOutcome.Passed;
            }

            ApplyHitToTarget(damageable, other, hitPosition, travelDirection);

            // 신규 적대 접촉은 피해 수락 여부와 무관하게 적중 기회를 소비합니다.
            if (remainingPierceCount > 0)
            {
                remainingPierceCount--;
                return ContactOutcome.Pierced;
            }

            isColliding = true;
            return ContactOutcome.Stopped;
        }

        if (IsBlockingEnvironment(other))
        {
            // 환경 차단은 대상 관통과 별개이므로 남은 횟수와 관계없이 종료합니다.
            isColliding = true;
            PlayHitEffect(hitPosition);
            return ContactOutcome.Stopped;
        }

        return ContactOutcome.Passed;
    }

    // 적대 대상의 전투 결과를 확정하고 무기 이벤트와 넉백 판단에 전달합니다.
    private void ApplyHitToTarget(IDamageable damageable, Collider2D targetCollider, Vector2 hitPosition, Vector2 travelDirection)
    {
        hitTargets.Add(damageable);

        // 방어 정책이 피해를 거부해도 이 대상과의 접촉 결과는 기록합니다.
        int finalDamage = CombatDamageMath.ApplyHit(damage, damageSnapshot, out bool isCritical);
        DamageResult damageResult = damageable.TryTakeDamage(
            new DamageInfo(attackerFaction, attacker, finalDamage, isParryable: true, incomingDirection: travelDirection));

        CombatResult combatResult = new CombatResult(
            damageable, weapon, finalDamage, damageResult, isCritical, hitPosition);
        LastCombatResult = combatResult;

        if (weapon != null)
        {
            weapon.TryPublishHit(new WeaponHitEvent(WeaponHitKind.Normal, combatResult, shotId));
        }

        if (damageResult.WasApplied)
        {
            ApplyKnockbackIfConfigured(targetCollider, travelDirection);
        }

        PlayHitEffect(hitPosition);
    }

    // 파괴된 Unity 객체가 인터페이스 참조에 남아 있어도 유효한 피해 대상으로 취급하지 않습니다.
    private IDamageable GetValidDamageable(Collider2D other)
    {
        Hurtbox hurtbox = other.GetComponent<Hurtbox>();
        IDamageable damageable = hurtbox != null ? hurtbox.Damageable : null;

        if (damageable is Object damageableObject && damageableObject == null)
        {
            return null;
        }

        return damageable;
    }

    private bool IsBlockingEnvironment(Collider2D other)
    {
        return other != null && other.gameObject.layer == wallLayer;
    }

    // IgnoreCollision이 Cast 결과를 항상 제외하지 않으므로 의미 판정에서도 다시 걸러냅니다.
    private bool IsIgnoredCollider(Collider2D other)
    {
        return ignoredColliders.Contains(other)
            || (projectileCollider != null && Physics2D.GetIgnoreCollision(projectileCollider, other));
    }

    private bool HasUnverifiedMovement()
    {
        return (transform.position - lastVerifiedPosition).sqrMagnitude
            > UnverifiedMovementTolerance * UnverifiedMovementTolerance;
    }


    // 유효한 설정과 수신자가 모두 있을 때만 실제 피해 대상에 넉백을 전달합니다.
    private void ApplyKnockbackIfConfigured(Collider2D collision, Vector2 direction)
    {
        if (knockbackSpeed <= 0f || knockbackDuration <= 0f)
            return;

        KnockbackReceiver receiver = collision.GetComponentInParent<KnockbackReceiver>();
        receiver?.ApplyKnockback(direction, knockbackSpeed, knockbackDuration);
    }

    // 중립 진영끼리는 서로 아군으로 간주하지 않습니다.
    private bool IsSameFaction(Faction targetFaction)
    {
        return attackerFaction != Faction.Neutral && targetFaction == attackerFaction;
    }

    /// <summary>
    /// 발사 시점의 스냅샷으로 풀 객체의 상태를 초기화하고 비행을 시작합니다.
    /// </summary>
    public void Launch(ProjectileLaunchRequest request)
    {
        ProjectileDataSO projectileData = request.Definition;

        projectileDefinition = projectileData;

        isColliding = false;
        hitTargets.Clear();
        remainingPierceCount = request.PierceCount;
        damage = request.Damage;
        attacker = request.Attacker;
        attackerFaction = request.SourceFaction;
        weapon = request.Weapon;
        damageSnapshot = request.DamageSnapshot;
        knockbackSpeed = request.KnockbackSpeed;
        knockbackDuration = request.KnockbackDuration;
        shotId = request.ShotId;
        LastCombatResult = null;

        UpdateIgnoredColliders(request.Attacker, request.SourceCollider);

        ConfigureSegmentSweep();

        ApplyFireDirection(request.AimAngle);

        spriteRenderer.sprite = projectileData.projectileSprite;
        projectileChargeTimer = Mathf.Max(0f, projectileData.projectileChargeTime);
        projectileRange = request.Range;
        projectileSpeed = request.Speed;
        overrideMovement = request.OverrideMovement;

        // 이전 대여의 이동 거리가 이번 발사의 사거리를 소비하지 않도록 스폰 위치에서 다시 측정합니다.
        lastVerifiedPosition = transform.position;
        accumulatedDistance = 0f;

        gameObject.SetActive(true);

        _trailRenderer.Clear();

        if (projectileData.isProjectileTrail)
        {
            _trailRenderer.gameObject.SetActive(true);
            _trailRenderer.emitting = true;
            _trailRenderer.startWidth = projectileData.projectileTrailStartWidth;
            _trailRenderer.endWidth = projectileData.projectileTrailEndWidth;
            _trailRenderer.time = projectileData.projectileTrailTime;
        }
        else
        {
            _trailRenderer.emitting = false;
            _trailRenderer.gameObject.SetActive(false);
        }

    }

    // 현재 레이어의 충돌 행렬과 Trigger를 포함하는 구간 검사 조건을 이번 대여에 맞춰 준비합니다.
    private void ConfigureSegmentSweep()
    {
        sweepHits.Clear();

        sweepEnabled = TryResolveSweepCollider();

        if (!sweepEnabled)
        {
            return;
        }

        sweepContactFilter = new ContactFilter2D
        {
            useTriggers = true,
            useLayerMask = true,
            layerMask = Physics2D.GetLayerCollisionMask(gameObject.layer),
        };
    }

    // 임의 형상 선택을 피하기 위해 활성 Collider 하나와 연결된 Rigidbody2D를 구간 검사의 전제로 검증합니다.
    private bool TryResolveSweepCollider()
    {
        GetComponents(ownColliders);

        Collider2D resolvedCollider = null;
        int enabledColliderCount = 0;

        for (int i = 0; i < ownColliders.Count; i++)
        {
            if (!ownColliders[i].enabled)
            {
                continue;
            }

            enabledColliderCount++;
            resolvedCollider = ownColliders[i];
        }

        if (enabledColliderCount != 1)
        {
            Logger.LogError($"{name}: 경로 충돌 검사는 활성 Collider2D가 정확히 1개여야 합니다(현재 {enabledColliderCount}개). 구간 검사를 생략합니다.", this);
            return false;
        }

        projectileCollider = resolvedCollider;

        if (projectileCollider.attachedRigidbody == null)
        {
            Logger.LogError($"{name}: 경로 충돌 검사는 Collider2D에 연결된 Rigidbody2D(Kinematic)가 필요합니다. 구간 검사를 생략합니다.", this);
            return false;
        }

        return true;
    }

    // 이전 발사의 충돌 무시를 원복한 뒤 이번 공격자 계층과 발사 주체를 모두 제외합니다.
    private void UpdateIgnoredColliders(Component newAttacker, Collider2D sourceCollider)
    {
        ClearIgnoredColliders();

        if (newAttacker != null)
        {
            Collider2D[] attackerColliders = newAttacker.GetComponentsInChildren<Collider2D>(true);

            for (int i = 0; i < attackerColliders.Length; i++)
            {
                IgnoreCollider(attackerColliders[i]);
            }
        }

        if (sourceCollider != null && !ignoredColliders.Contains(sourceCollider))
        {
            IgnoreCollider(sourceCollider);
        }
    }

    // 물리 설정을 적용한 대상도 기록해 비활성화 시 반드시 원복합니다.
    private void IgnoreCollider(Collider2D collider)
    {
        if (projectileCollider != null)
        {
            Physics2D.IgnoreCollision(projectileCollider, collider, true);
        }

        ignoredColliders.Add(collider);
    }

    // 풀에 반환하기 전에 이번 발사가 설정한 충돌 무시를 모두 원복합니다.
    private void ClearIgnoredColliders()
    {
        for (int i = 0; i < ignoredColliders.Count; i++)
        {
            if (ignoredColliders[i] != null && projectileCollider != null)
            {
                Physics2D.IgnoreCollision(projectileCollider, ignoredColliders[i], false);
            }
        }

        ignoredColliders.Clear();
    }

    private void ApplyFireDirection(float aimAngle)
    {
        fireDirectionAngle = aimAngle;

        transform.eulerAngles = new Vector3(0f, 0f, fireDirectionAngle);

        fireDirectionVector = DirectionUtility2D.DirectionFromDegrees(fireDirectionAngle);

    }

    // 풀 소유 객체는 반환하고, 대형의 자식처럼 풀 밖 객체는 안전하게 비활성화합니다.
    private void ReturnOrDisable()
    {
        if (PoolManager.HasInstance && PoolManager.Instance.ReturnComponent(this))
        {
            return;
        }

        gameObject.SetActive(false);
    }

    // 적중 연출은 전투 결과를 바꾸지 않으며 설정이나 풀을 사용할 수 없으면 생략합니다.
    private void PlayHitEffect(Vector3 hitPosition)
    {
        if (projectileDefinition == null)
        {
            return;
        }

        EffectDefinitionSO projectileHitSpriteEffect = projectileDefinition.projectileHitSpriteEffect;

        if (projectileHitSpriteEffect == null || projectileHitSpriteEffect.effectViewPrefab == null)
        {
            return;
        }

        if (!PoolManager.HasInstance)
        {
            return;
        }

        Component rentedComponent = PoolManager.Instance.RentComponent(projectileHitSpriteEffect.effectViewPrefab, hitPosition, Quaternion.identity);

        EffectView effectView = rentedComponent as EffectView;

        if (effectView == null)
        {
            PoolManager.Instance.ReturnComponent(rentedComponent);
            return;
        }

        effectView.Play(projectileHitSpriteEffect);
    }

    public GameObject GetGameObject()
    {
        return gameObject;
    }

}
