
using UnityEngine;

public sealed class ShieldOutlineView : MonoBehaviour
{
    private static readonly int OutlineAlphaId =
        Shader.PropertyToID("_OutlineAlpha");

    [SerializeField] private Shield shield;
    [SerializeField] private SpriteRenderer spriteRenderer;

    private Material runtimeMaterial;
    private bool previousShieldActive;

    private void Awake()
    {
        runtimeMaterial = spriteRenderer.material;
        previousShieldActive = false;

        UpdateOutline();
    }

    private void LateUpdate()
    {
        if (previousShieldActive == shield.IsActive)
            return;

        UpdateOutline();
    }

    // 현재 실드 보유 여부에 따라 외곽선 표시 상태를 갱신한다.
    private void UpdateOutline()
    {
        bool isActive = shield.IsActive;

        runtimeMaterial.SetFloat(
            OutlineAlphaId,
            isActive ? 1f : 0f
        );

        previousShieldActive = isActive;
    }

    private void OnDestroy()
    {
        if (runtimeMaterial != null)
            Destroy(runtimeMaterial);
    }
}

