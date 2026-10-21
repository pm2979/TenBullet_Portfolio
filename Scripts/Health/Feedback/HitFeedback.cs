using DG.Tweening;
using UnityEngine;

/// <summary>
/// 피해가 성공적으로 적용됐을 때만 짧은 피격 플래시를 재생하는 컴포넌트.
/// 체력 수치·무적 상태·소유자 타입은 알지 못하며, <see cref="Configure"/>로 대상 Health와 SpriteRenderer를 주입받는다.
/// 연출은 SpriteRenderer의 색이 아니라 All In 1 Sprite Shader의 Hit Effect 강도로 표현해 다른 색상 연출과 충돌하지 않는다
/// </summary>
public sealed class HitFeedback : MonoBehaviour
{
    private static readonly int HitEffectBlendId = Shader.PropertyToID("_HitEffectBlend");
    private static readonly int HitEffectColorId = Shader.PropertyToID("_HitEffectColor");

    // 복귀 곡선은 연출 편차가 크지 않아 상수로 둔다. 대상별 조정이 필요해지면 그때 직렬화한다
    private const Ease RecoveryEase = Ease.OutQuad;

    [Header("피격 플래시")]
    [Tooltip("플래시로 덮어쓸 색. Material의 Hit Effect Color로 적용한다")]
    [SerializeField] private Color _flashColor = Color.red;

    [Tooltip("최대 강도에서 0까지 복귀하는 데 걸리는 시간(초)")]
    [Min(0.01f)]
    [SerializeField] private float _flashDuration = 0.18f;

    [Tooltip("피격 순간의 Hit Effect 최대 강도(0=연출 없음, 1=완전히 덮어쓰기)")]
    [Range(0f, 1f)]
    [SerializeField] private float _flashIntensity = 1f;

    private Health health;
    private SpriteRenderer spriteRenderer;
    private bool isSubscribed = false;

    // 대상 Renderer의 Material 인스턴스. Renderer 인스턴스는 Renderer가 파괴돼도 자동으로 해제되지 않으므로,
    // 이 컴포넌트가 직접 만든 경우(ownsFlashMaterial)에만 최종 파괴 시 해제한다.
    // 이미 다른 컴포넌트(ShieldOutlineView 등)가 만들어 둔 인스턴스는 공유만 하고 소유하지 않는다
    private Material flashMaterial;
    private bool ownsFlashMaterial;
    private bool hasWarnedMissingSupport;

    private Tweener flashTween;

    // SetRecyclable로 재활용된 Tween을 이전 참조로 오인해 Kill하지 않도록, 종료 시 참조를 끊는다.
    // 피격마다 대리자를 새로 만들지 않도록 한 번만 생성해 보관한다
    private TweenCallback onFlashTweenKilled;

    private void Awake()
    {
        onFlashTweenKilled = () => flashTween = null;
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void OnDisable()
    {
        // 풀 반환 경로다. Material은 다음 대여에서 그대로 재사용하므로 해제하지 않는다
        Teardown();
    }

    private void OnDestroy()
    {
        Teardown();
        ReleaseMaterial();
    }

    /// <summary>
    /// 플래시를 재생할 대상 Health와 SpriteRenderer를 연결한다.
    /// 활성 상태에서 재호출되면 이전 대상의 연출과 구독을 먼저 정리한 뒤 새 Health를 다시 구독한다.
    /// 둘 중 하나가 null이어도 예외 없이 처리하며, 이 경우 플래시를 재생하지 않는다
    /// </summary>
    public void Configure(Health health, SpriteRenderer spriteRenderer)
    {
        // 풀 재대여처럼 같은 대상으로 다시 연결되는 경우다. 연출만 초기화하고 Material 작업을 건너뛴다
        if (this.health == health && this.spriteRenderer == spriteRenderer)
        {
            StopFlash();

            if (isActiveAndEnabled)
                Subscribe();

            return;
        }

        // 참조를 교체하기 전에 정리해야 이전 Renderer에 플래시 강도가 남지 않는다
        Teardown();

        // 이전 Renderer는 계속 그 Material로 그려지므로 파괴하지 않고 참조만 놓는다
        flashMaterial = null;
        ownsFlashMaterial = false;
        hasWarnedMissingSupport = false;

        this.health = health;
        this.spriteRenderer = spriteRenderer;

        AcquireMaterial();

        if (isActiveAndEnabled)
            Subscribe();
    }

    // 현재 대상 기준으로 재생 중인 연출과 구독을 함께 정리한다. 비활성화·파괴·대상 교체가 모두 이 경로를 쓴다
    private void Teardown()
    {
        StopFlash();
        Unsubscribe();
    }

    // 대상 Renderer에서 Hit Effect를 제어할 Material 인스턴스를 확보한다.
    // Hit Effect를 지원하지 않는 Material이면 공유 Material을 건드리지 않고 재생을 생략한다
    private void AcquireMaterial()
    {
        if (spriteRenderer == null)
            return;

        Material sharedMaterial = spriteRenderer.sharedMaterial;

        if (IsHitEffectSupported(sharedMaterial) == false)
        {
            WarnMissingSupport();
            return;
        }

        // 이미 인스턴스화된 Renderer면 같은 인스턴스를 그대로 돌려받는다.
        // 새 인스턴스가 만들어진 경우에만 이 컴포넌트가 해제 책임을 진다
        Material instance = spriteRenderer.material;

        ownsFlashMaterial = instance != sharedMaterial;
        flashMaterial = instance;
        flashMaterial.SetFloat(HitEffectBlendId, 0f);
    }

    // 이 컴포넌트가 만든 Material만 해제한다. 최종 파괴 시점에만 호출한다
    private void ReleaseMaterial()
    {
        if (ownsFlashMaterial && flashMaterial != null)
            Destroy(flashMaterial);

        flashMaterial = null;
        ownsFlashMaterial = false;
    }

    private static bool IsHitEffectSupported(Material material)
    {
        return material != null
            && material.HasProperty(HitEffectBlendId)
            && material.HasProperty(HitEffectColorId);
    }

    // 설정 누락은 Prefab에서 고쳐야 하는 문제다. 풀 재대여마다 같은 경고가 쌓이지 않도록 대상당 한 번만 남긴다
    private void WarnMissingSupport()
    {
        if (hasWarnedMissingSupport)
            return;

        hasWarnedMissingSupport = true;

        Logger.LogWarning(
            $"{nameof(HitFeedback)}: '{name}'의 SpriteRenderer Material이 Hit Effect를 지원하지 않아 피격 플래시를 재생하지 않습니다.",
            this);
    }

    private void Subscribe()
    {
        if (isSubscribed || health == null)
            return;

        health.Changed += OnHealthChanged;
        isSubscribed = true;
    }

    private void Unsubscribe()
    {
        if (isSubscribed == false)
            return;

        // 대상 Health가 먼저 파괴된 경우에도 구독 상태만 되돌린다
        if (health != null)
            health.Changed -= OnHealthChanged;

        isSubscribed = false;
    }

    private void OnHealthChanged(HealthChange change)
    {
        if (change.Reason == HealthChangeReason.Damage && change.Delta < 0)
            Play();
    }

    private void Play()
    {
        if (flashMaterial == null)
            return;

        // 연속 피격은 진행 중인 복귀를 중단하고 최대 강도에서 다시 시작한다(동시에 살아 있는 Tween은 항상 하나)
        KillTween();

        flashMaterial.SetColor(HitEffectColorId, _flashColor);
        flashMaterial.SetFloat(HitEffectBlendId, _flashIntensity);

        flashTween = flashMaterial
            .DOFloat(0f, HitEffectBlendId, _flashDuration)
            .SetEase(RecoveryEase)
            .SetRecyclable(true)
            .SetLink(gameObject)
            .OnKill(onFlashTweenKilled);
    }

    // 재생 중인 플래시를 중단하고 Hit Effect 강도를 평상시 값으로 되돌린다
    private void StopFlash()
    {
        KillTween();

        if (flashMaterial != null)
            flashMaterial.SetFloat(HitEffectBlendId, 0f);
    }

    private void KillTween()
    {
        if (flashTween != null && flashTween.IsActive())
            flashTween.Kill();

        flashTween = null;
    }
}
